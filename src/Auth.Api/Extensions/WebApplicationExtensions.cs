using Auth.Api.Contracts;
using Auth.Api.Middlewares;
using Auth.Api.Options;
using Auth.Api.Security;

using Microsoft.Extensions.Options;

namespace Auth.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Pipeline padrão do Auth.Api.
    /// </summary>
    public static WebApplication UseAuthApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<LoginRateLimitMiddleware>();
        return app;
    }

    public static WebApplication UseAuthApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        var swaggerEnabled = app.Environment.IsDevelopment() || configuration.GetValue<bool>("Swagger:Enabled");
        if (!swaggerEnabled)
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth.Api v1");
            c.RoutePrefix = string.Empty; // Swagger na raiz
        });

        return app;
    }

    /// <summary>
    /// Registra os endpoints do Auth.Api.
    /// </summary>
    public static WebApplication MapAuthApiEndpoints(this WebApplication app)
    {
        // --- Endpoints ---

        app.MapGet("/health", () => Results.Text("ok"))
            .WithName("Health")
            .WithSummary("Health check simples")
            .WithDescription("Retorna 200 com body 'ok'.");

        app.MapGet("/.well-known/jwks.json", (HttpContext httpContext, IRsaKeyProvider keys) =>
            {
                var rsa = keys.GetPublicKey();
                var p = rsa.ExportParameters(includePrivateParameters: false);
                if (p.Modulus is null || p.Exponent is null)
                    return Results.Problem("Chave pública inválida.");

                var jwks = new JwksResponse
                {
                    Keys =
                    [
                        new JwkKey
                        {
                            Kid = keys.GetKeyId(),
                            N = Base64Url(p.Modulus),
                            E = Base64Url(p.Exponent)
                        }
                    ]
                };

                httpContext.Response.Headers.CacheControl = "public, max-age=3600";

                return Results.Ok(jwks);
            })
            .WithName("Jwks")
            .WithSummary("JWKS público para validação offline de JWT")
            .WithDescription("Retorna a chave pública RSA atual em formato JWKS (kty,use,alg,kid,n,e). Não requer autenticação.");

        app.MapPost("/auth/login", (LoginRequest request, IJwtIssuer issuer, IOptions<AuthOptions> authOptionsAccessor, ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("Auth.Login");
                var authOptions = authOptionsAccessor.Value;

                // Validações mínimas
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    logger.LogWarning("Tentativa de login inválida (username/password vazios). username={Username}", request.Username);
                    return Results.Json(new ErrorResponse
                    {
                        Error = "invalid_credentials",
                        Message = "Usuário ou senha inválidos."
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }

                // Usuario configurado localmente para a POC.
                var validUser = authOptions.DevelopmentUser.Username;
                var validPass = authOptions.DevelopmentUser.Password;

                if (!string.Equals(request.Username, validUser, StringComparison.Ordinal) ||
                    !string.Equals(request.Password, validPass, StringComparison.Ordinal))
                {
                    // Segurança: nunca logar senha
                    logger.LogWarning("Tentativa de login inválida para username={Username}", request.Username);
                    return Results.Json(new ErrorResponse
                    {
                        Error = "invalid_credentials",
                        Message = "Usuário ou senha inválidos."
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }

                // Scope
                var requested = (request.Scope ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                string grantedScopes;

                if (requested.Length == 0)
                {
                    logger.LogWarning("Tentativa de login sem scope explicito para username={Username}", request.Username);
                    return Results.Json(new ErrorResponse
                    {
                        Error = "invalid_scope",
                        Message = $"Informe ao menos um scope explicito. Scopes validos: {ScopeCatalog.ValidScopesAsString()}"
                    }, statusCode: StatusCodes.Status400BadRequest);
                }
                else
                {
                    var invalid = requested.Where(s => !ScopeCatalog.ValidScopes.Contains(s, StringComparer.Ordinal)).ToArray();
                    if (invalid.Length > 0)
                    {
                        return Results.Json(new ErrorResponse
                        {
                            Error = "invalid_scope",
                            Message = $"Scopes inválidos: {string.Join(' ', invalid)}. Scopes válidos: {ScopeCatalog.ValidScopesAsString()}"
                        }, statusCode: StatusCodes.Status400BadRequest);
                    }

                    grantedScopes = string.Join(' ', requested);
                }

                var jwt = issuer.IssueAccessToken(
                    subject: validUser!,
                    preferredUsername: validUser!,
                    scopes: grantedScopes,
                    out var expiresAtUtc);

                logger.LogInformation("Login bem-sucedido para username={Username} scopes={Scopes}", validUser, grantedScopes);

                // Requisito: expira em 10 minutos (600s) por padrão.
                // Retornamos o tempo configurado, e não o delta calculado, para manter o contrato estável.
                var lifetimeMinutes = authOptions.TokenLifetimeMinutes <= 0 ? 10 : authOptions.TokenLifetimeMinutes;
                var expiresInSeconds = lifetimeMinutes * 60;

                return Results.Ok(new LoginResponse
                {
                    AccessToken = jwt,
                    TokenType = "Bearer",
                    ExpiresIn = expiresInSeconds,
                    Scope = grantedScopes
                });
            })
            .WithName("Login")
            .WithSummary("Login (PoC) - Emite JWT RS256 e retorna access_token")
            .WithDescription($"Usuario/senha de POC configurados via `Auth:DevelopmentUser`, sem fallback em producao.\n\nScopes validos: {ScopeCatalog.ValidScopesAsString()}.\n\nEnvie `scope` (string com scopes separados por espaco). `scope` vazio/nulo e rejeitado.\n\nO token inclui `merchant_id` com os merchants configurados em Auth:AuthorizedMerchants.\n\nO token expira em 10 minutos (configuravel) e nao ha refresh token/revogacao/logout.")
            .Accepts<LoginRequest>("application/json")
            .Produces<LoginResponse>(StatusCodes.Status200OK, "application/json")
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, "application/json")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest, "application/json")
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests, "application/json");

        return app;
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
