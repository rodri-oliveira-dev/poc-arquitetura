namespace LedgerService.Api.Options;

/// <summary>
/// Configurações de autenticação/validação de JWT.
/// </summary>
public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Issuer (iss) esperado.
    /// Ex.: http://localhost:8081/realms/poc
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Audience (aud) esperada para este serviço.
    /// Ex.: ledger-api
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// URL direta do JWKS do emissor configurado.
    /// Ex.: http://localhost:8081/realms/poc/protocol/openid-connect/certs
    /// </summary>
    public string JwksUrl { get; init; } = string.Empty;

    public bool RequireHttpsMetadata { get; init; } = true;

    public int JwksTimeoutSeconds { get; init; } = 5;
    public int JwksRetryCount { get; init; } = 2;
    public int JwksRetryBaseDelayMilliseconds { get; init; } = 200;
}
