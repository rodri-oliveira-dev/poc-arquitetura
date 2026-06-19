using ApiDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApiDefaults.Extensions;

public static class JwtAuthenticationServiceCollectionExtensions
{
    private static readonly Action<ILogger, PathString, Exception?> _logAuthenticationFailed =
        LoggerMessage.Define<PathString>(
            LogLevel.Warning,
            new EventId(1, "LogAuthenticationFailed"),
            "JWT authentication failed. path={Path}");

    public static IServiceCollection AddApiJwtBearerAuthentication(
        this IServiceCollection services,
        ApiJwtAuthenticationOptions jwtOptions,
        IHostEnvironment environment,
        Action<AuthorizationOptions> configureAuthorization)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jwtOptions);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configureAuthorization);

        ValidateRequired(jwtOptions.SectionName, nameof(jwtOptions.Issuer), jwtOptions.Issuer);
        ValidateRequired(jwtOptions.SectionName, nameof(jwtOptions.Audience), jwtOptions.Audience);
        ValidateRequired(jwtOptions.SectionName, nameof(jwtOptions.JwksUrl), jwtOptions.JwksUrl);
        ValidateTransport(jwtOptions, environment);

        IConfigurationManager<OpenIdConnectConfiguration> configurationManager =
            new ConfigurationManager<OpenIdConnectConfiguration>(
                jwtOptions.JwksUrl,
                new JwksConfigurationRetriever(),
                new RetryableJwksDocumentRetriever(jwtOptions));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                options.ConfigurationManager = configurationManager;
                options.TokenValidationParameters = BuildTokenValidationParameters(jwtOptions);
                options.Events = BuildJwtBearerEvents();
            });

        services.AddAuthorization(configureAuthorization);

        return services;
    }

    public static IServiceCollection AddConfiguredApiJwtBearerAuthentication<TService, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string sectionName,
        Action<AuthorizationOptions> configureAuthorization)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        services.AddSingleton<TService, TImplementation>();

        return services.AddApiJwtBearerAuthentication(
            ReadOptions(configuration.GetSection(sectionName), sectionName),
            environment,
            configureAuthorization);
    }

    public static AuthorizationOptions RequireAuthenticatedUserByDefault(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        return options;
    }

    private static TokenValidationParameters BuildTokenValidationParameters(ApiJwtAuthenticationOptions options)
        => new()
        {
            NameClaimType = "preferred_username",
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            AudienceValidator = (audiences, _, _) => ContainsAudience(audiences, options.Audience),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

    private static ApiJwtAuthenticationOptions ReadOptions(IConfiguration configuration, string sectionName)
        => new(
            sectionName,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.Issuer)) ?? string.Empty,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.Audience)) ?? string.Empty,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.JwksUrl)) ?? string.Empty,
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.RequireHttpsMetadata), true),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksTimeoutSeconds), 5),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksRetryCount), 2),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksRetryBaseDelayMilliseconds), 200));

    private static JwtBearerEvents BuildJwtBearerEvents()
        => new()
        {
            OnAuthenticationFailed = context =>
            {
                ILogger logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth");

                _logAuthenticationFailed(logger, context.HttpContext.Request.Path, context.Exception);

                return Task.CompletedTask;
            }
        };

    private static bool ContainsAudience(IEnumerable<string>? audiences, string expectedAudience)
    {
        if (audiences is null)
        {
            return false;
        }

        foreach (string audience in audiences)
        {
            string[] values = (audience ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (values.Contains(expectedAudience, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateRequired(string sectionName, string optionName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{sectionName}:{optionName} e obrigatorio.");
        }
    }

    private static void ValidateTransport(ApiJwtAuthenticationOptions options, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Local") || environment.IsEnvironment("Test"))
        {
            return;
        }

        if (!options.RequireHttpsMetadata)
        {
            throw new InvalidOperationException($"{options.SectionName}:RequireHttpsMetadata=false e permitido apenas em Development/Local.");
        }

        if (!Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out Uri? jwksUri)
            || !string.Equals(jwksUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{options.SectionName}:JwksUrl deve usar HTTPS fora de Development/Local.");
        }
    }

    private sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address,
            IDocumentRetriever retriever,
            CancellationToken cancel)
        {
            string json = await retriever.GetDocumentAsync(address, cancel);
            JsonWebKeySet keys = new(json);
            OpenIdConnectConfiguration configuration = new();

            foreach (SecurityKey signingKey in keys.GetSigningKeys())
            {
                configuration.SigningKeys.Add(signingKey);
            }

            return configuration;
        }
    }

    internal sealed class RetryableJwksDocumentRetriever(ApiJwtAuthenticationOptions options) : IDocumentRetriever
    {
        private static readonly HttpClient _client = new();
        private readonly TimeSpan _baseDelay = TimeSpan.FromMilliseconds(Math.Max(1, options.JwksRetryBaseDelayMilliseconds));
        private readonly int _retryCount = Math.Max(0, options.JwksRetryCount);
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(Math.Max(1, options.JwksTimeoutSeconds));

        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            for (int attempt = 0; attempt <= _retryCount; attempt++)
            {
                try
                {
                    using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                    timeout.CancelAfter(_timeout);

                    using HttpResponseMessage response = await _client.GetAsync(address, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(timeout.Token);
                }
                catch (Exception ex) when (ShouldRetry(ex, attempt, cancel))
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "JWKS fetch failed. Retrying attempt {0}/{1}. Error: {2}",
                        attempt + 1,
                        _retryCount + 1,
                        ex.Message);

                    double backoff = _baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(backoff), cancel);
                }
            }

            throw new InvalidOperationException("JWKS fetch retry loop completed without returning a document.");
        }

        private bool ShouldRetry(Exception exception, int attempt, CancellationToken cancellationToken)
            => !cancellationToken.IsCancellationRequested
                && attempt < _retryCount
                && exception is HttpRequestException or TaskCanceledException;
    }
}
