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
    public static IServiceCollection AddApiJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtAuthOptions>()
            .Bind(configuration.GetSection(JwtAuthOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtAuthOptions.SectionName).Get<JwtAuthOptions>()
            ?? new JwtAuthOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:Issuer é obrigatório.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:Audience é obrigatório.");

        if (string.IsNullOrWhiteSpace(jwtOptions.JwksUrl))
            throw new InvalidOperationException($"{JwtAuthOptions.SectionName}:JwksUrl é obrigatório.");

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
                options.RequireHttpsMetadata = false;

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
        private readonly HttpClient _client;
        private readonly int _retryCount;
        private readonly TimeSpan _retryBaseDelay;

        public ResilientJwksDocumentRetriever(JwtAuthOptions options)
        {
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.JwksTimeoutSeconds))
            };
            _retryCount = Math.Max(0, options.JwksRetryCount);
            _retryBaseDelay = TimeSpan.FromMilliseconds(Math.Max(1, options.JwksRetryBaseDelayMilliseconds));
        }

        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var response = await _client.GetAsync(address, cancel);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(cancel);
                }
                catch (Exception ex) when (attempt < _retryCount && !cancel.IsCancellationRequested)
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
