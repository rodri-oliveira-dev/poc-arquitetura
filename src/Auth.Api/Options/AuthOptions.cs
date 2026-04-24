namespace Auth.Api.Options;

/// <summary>
/// Configurações de emissão de tokens JWT (issuer, audiences, tempo de vida e persistência da chave RSA).
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Issuer (iss) do JWT.
    /// Ex.: https://auth-api
    /// </summary>
    public string Issuer { get; init; } = "https://auth-api";

    /// <summary>
    /// Lista de audiences aceitas (aud). Na emissão, o claim aud é gerado como string com espaços,
    /// pois esta POC trabalha com múltiplos alvos de forma simples.
    /// </summary>
    public string[] Audiences { get; init; } = [];

    /// <summary>
    /// Merchants autorizados para o usuario fixo desta POC. Emitidos na claim merchant_id.
    /// </summary>
    public string[] AuthorizedMerchants { get; init; } = [];

    /// <summary>
    /// Tempo de vida do access token em minutos.
    /// </summary>
    public int TokenLifetimeMinutes { get; init; } = 10;

    /// <summary>
    /// Caminho do arquivo para persistência da chave RSA (evita invalidar tokens entre reinícios).
    /// </summary>
    public string KeyPath { get; init; } = "./data/keys/auth-rsa-key.json";

    /// <summary>
    /// Credencial local da POC usada pelo endpoint de login.
    /// Deve ser configurada por ambiente; nao ha fallback seguro para producao.
    /// </summary>
    public AuthDevelopmentUserOptions DevelopmentUser { get; init; } = new();

    /// <summary>
    /// Rate limit aplicado ao endpoint de login.
    /// </summary>
    public AuthLoginRateLimitOptions LoginRateLimit { get; init; } = new();
}

public sealed class AuthDevelopmentUserOptions
{
    public string? Username { get; init; }

    public string? Password { get; init; }
}

public sealed class AuthLoginRateLimitOptions
{
    public int PermitLimit { get; init; } = 10;

    public int WindowSeconds { get; init; } = 60;
}
