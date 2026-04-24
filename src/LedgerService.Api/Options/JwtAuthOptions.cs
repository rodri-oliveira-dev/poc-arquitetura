namespace LedgerService.Api.Options;

/// <summary>
/// Configurações de autenticação/validação de JWT (consumo de tokens emitidos pelo Auth.Api).
/// </summary>
public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Issuer (iss) esperado.
    /// Ex.: https://auth-api
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Audience (aud) esperada para este serviço.
    /// Ex.: ledger-api
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// URL do JWKS do Auth.Api.
    /// Ex.: http://localhost:5030/.well-known/jwks.json
    /// </summary>
    public string JwksUrl { get; init; } = string.Empty;

    public int JwksTimeoutSeconds { get; init; } = 5;
    public int JwksRetryCount { get; init; } = 2;
    public int JwksRetryBaseDelayMilliseconds { get; init; } = 200;
}
