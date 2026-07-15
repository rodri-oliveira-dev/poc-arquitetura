using System.Globalization;

using ApiDefaults.Authentication;

using HttpResilienceDefaults;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApiDefaults.Extensions;

public static class JwtAuthenticationServiceCollectionExtensions
{
    internal const string JwksHttpClientName = "JWKS";

    private static readonly Action<ILogger, PathString, Exception?> _logAuthenticationFailed =
        LoggerMessage.Define<PathString>(
            LogLevel.Warning,
            new EventId(1, "LogAuthenticationFailed"),
            "JWT authentication failed. path={Path}");

    public static IServiceCollection AddApiJwtBearerAuthentication(
        this IServiceCollection services,
        ApiJwtAuthenticationOptions jwtOptions,
        IHostEnvironment environment,
        Action<AuthorizationOptions> configureAuthorization,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jwtOptions);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configureAuthorization);

        var validator = new ApiJwtAuthenticationOptionsValidator(environment);
        ValidateOptionsResult validation = validator.Validate(name: null, jwtOptions);
        if (validation.Failed)
        {
            throw new InvalidOperationException(string.Join(" ", validation.Failures));
        }

        services.AddSingleton<IValidateOptions<ApiJwtAuthenticationOptions>>(validator);

        IConfiguration jwksResilienceConfiguration = BuildJwksResilienceConfiguration(jwtOptions, configuration);
        services
            .AddHttpClient(JwksHttpClientName)
            .AddConfiguredHttpResilience(jwksResilienceConfiguration, JwksHttpClientName);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                options.TokenValidationParameters = BuildTokenValidationParameters(jwtOptions);
                options.Events = BuildJwtBearerEvents();
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, httpClientFactory) =>
            {
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    jwtOptions.JwksUrl.Trim(),
                    new JwksConfigurationRetriever(),
                    new JwksHttpClientDocumentRetriever(httpClientFactory, JwksHttpClientName));
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
            configureAuthorization,
            configuration);
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
            ValidIssuer = options.Issuer.Trim(),
            ValidateAudience = true,
            AudienceValidator = (audiences, _, _) => ContainsAudience(audiences, options.Audience.Trim()),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

    private static ApiJwtAuthenticationOptions ReadOptions(IConfiguration configuration, string sectionName)
        => new(
            sectionName,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.Issuer))?.Trim() ?? string.Empty,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.Audience))?.Trim() ?? string.Empty,
            configuration.GetValue<string>(nameof(ApiJwtAuthenticationOptions.JwksUrl))?.Trim() ?? string.Empty,
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.RequireHttpsMetadata), true),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksTimeoutSeconds), 5),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksRetryCount), 2),
            configuration.GetValue(nameof(ApiJwtAuthenticationOptions.JwksRetryBaseDelayMilliseconds), 200))
        {
            ClockSkewSeconds = configuration.GetValue(nameof(ApiJwtAuthenticationOptions.ClockSkewSeconds), 30)
        };

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

    private static IConfiguration BuildJwksResilienceConfiguration(
        ApiJwtAuthenticationOptions jwtOptions,
        IConfiguration? configuration)
    {
        int retryCount = Math.Max(1, jwtOptions.JwksRetryCount);
        TimeSpan attemptTimeout = TimeSpan.FromSeconds(Math.Max(1, jwtOptions.JwksTimeoutSeconds));
        TimeSpan retryDelay = TimeSpan.FromMilliseconds(Math.Max(1, jwtOptions.JwksRetryBaseDelayMilliseconds));
        TimeSpan totalTimeout = TimeSpan.FromTicks(
            (attemptTimeout.Ticks * (retryCount + 1)) + (retryDelay.Ticks * retryCount));

        Dictionary<string, string?> defaults = new(StringComparer.OrdinalIgnoreCase)
        {
            [$"{HttpClientResilienceOptions.SectionName}:Clients:{JwksHttpClientName}:TotalTimeout"] = totalTimeout.ToString("c"),
            [$"{HttpClientResilienceOptions.SectionName}:Clients:{JwksHttpClientName}:AttemptTimeout"] = attemptTimeout.ToString("c"),
            [$"{HttpClientResilienceOptions.SectionName}:Clients:{JwksHttpClientName}:RetryCount"] = retryCount.ToString(CultureInfo.InvariantCulture),
            [$"{HttpClientResilienceOptions.SectionName}:Clients:{JwksHttpClientName}:RetryDelay"] = retryDelay.ToString("c")
        };

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults);

        if (configuration is not null)
        {
            builder.AddConfiguration(configuration);
        }

        return builder.Build();
    }

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

    internal sealed class JwksHttpClientDocumentRetriever(
        IHttpClientFactory httpClientFactory,
        string clientName) : IDocumentRetriever
    {
        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            HttpClient client = httpClientFactory.CreateClient(clientName);
            using HttpRequestMessage request = new(HttpMethod.Get, address);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancel);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancel);
        }
    }
}
