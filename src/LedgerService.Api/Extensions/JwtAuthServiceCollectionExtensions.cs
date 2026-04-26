using LedgerService.Api.Options;
using LedgerService.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LedgerService.Api.Extensions;

public static class JwtAuthServiceCollectionExtensions
{
    public static IServiceCollection AddApiJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName));
        services.AddSingleton<IMerchantAuthorizationService, MerchantAuthorizationService>();

        var jwtOptions = configuration.GetSection(JwtAuthOptions.SectionName).Get<JwtAuthOptions>()
            ?? new JwtAuthOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:Issuer é obrigatório.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:Audience é obrigatório.");

        if (string.IsNullOrWhiteSpace(jwtOptions.JwksUrl))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:JwksUrl é obrigatório.");

        ValidateTransportSecurity(jwtOptions, environment);

        // Requisito: não chamar Auth.Api a cada request.
        // Usamos ConfigurationManager com cache e refresh automático.
        var jwksManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress: jwtOptions.JwksUrl,
            configRetriever: new JwksOnlyConfigurationRetriever(),
            docRetriever: new ResilientJwksDocumentRetriever(jwtOptions));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Compatibilidade
                    NameClaimType = "preferred_username",

                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    // Observação de compatibilidade:
                    // O Auth.Api atual emite "aud" como 1 string com múltiplas audiences separadas por espaço
                    // (ex.: "ledger-api balance-api"). O JwtSecurityTokenHandler trata isso como UMA audiência.
                    // Portanto, validamos manualmente tokenizando por espaço.
                    AudienceValidator = (audiences, _, _) =>
                    {
                        if (audiences is null)
                            return false;

                        return audiences
                            .SelectMany(a => (a ?? string.Empty)
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            .Contains(jwtOptions.Audience, StringComparer.Ordinal);
                    },

                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    // Força RS256 para tokens desta POC.
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                };

                options.ConfigurationManager = jwksManager;

                // Telemetria útil para debug (não loga token).
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth")
                            .LogWarning(ctx.Exception, "JWT authentication failed. path={Path}", ctx.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            // Requisito: policies por scope.
            options.AddScopePolicies();

            // Requisito: exigir autenticação por padrão nas rotas de negócio.
            // Swagger fica público pois está antes do UseAuthorization no pipeline.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static void ValidateTransportSecurity(JwtAuthOptions options, IHostEnvironment environment)
    {
        if (IsLocalEnvironment(environment))
            return;

        if (!options.RequireHttpsMetadata)
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:RequireHttpsMetadata=false é permitido apenas em Development/Local.");

        if (!Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var jwksUri)
            || !string.Equals(jwksUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:JwksUrl deve usar HTTPS fora de Development/Local.");
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");

    /// <summary>
    /// Recupera e converte JWKS (/.well-known/jwks.json) para OpenIdConnectConfiguration.
    /// </summary>
    private sealed class JwksOnlyConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            return GetAsync(address, retriever, cancel);
        }

        private static async Task<OpenIdConnectConfiguration> GetAsync(string jwksAddress, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var json = await retriever.GetDocumentAsync(jwksAddress, cancel);
            var jwks = new JsonWebKeySet(json);

            var config = new OpenIdConnectConfiguration();
            foreach (var key in jwks.GetSigningKeys())
                config.SigningKeys.Add(key);

            return config;
        }
    }

    private sealed class ResilientJwksDocumentRetriever : IDocumentRetriever
    {
        private static readonly HttpClient Client = new();
        private readonly TimeSpan _timeout;
        private readonly int _retryCount;
        private readonly TimeSpan _retryBaseDelay;

        public ResilientJwksDocumentRetriever(JwtAuthOptions options)
        {
            _timeout = TimeSpan.FromSeconds(Math.Max(1, options.JwksTimeoutSeconds));
            _retryCount = Math.Max(0, options.JwksRetryCount);
            _retryBaseDelay = TimeSpan.FromMilliseconds(Math.Max(1, options.JwksRetryBaseDelayMilliseconds));
        }

        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                    timeout.CancelAfter(_timeout);
                    using var response = await Client.GetAsync(address, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(timeout.Token);
                }
                catch (HttpRequestException ex) when (attempt < _retryCount && !cancel.IsCancellationRequested)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "JWKS fetch failed. Retrying attempt {0}/{1}. Error: {2}",
                        attempt + 1,
                        _retryCount + 1,
                        ex.Message);
                    await Task.Delay(TimeSpan.FromMilliseconds(_retryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt)), cancel);
                }
                catch (TaskCanceledException ex) when (attempt < _retryCount && !cancel.IsCancellationRequested)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "JWKS fetch failed. Retrying attempt {0}/{1}. Error: {2}",
                        attempt + 1,
                        _retryCount + 1,
                        ex.Message);
                    await Task.Delay(TimeSpan.FromMilliseconds(_retryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt)), cancel);
                }
            }
        }
    }
}
