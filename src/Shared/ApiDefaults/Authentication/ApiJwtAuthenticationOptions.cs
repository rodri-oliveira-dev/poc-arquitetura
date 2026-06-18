namespace ApiDefaults.Authentication;

public sealed record ApiJwtAuthenticationOptions(
    string SectionName,
    string Issuer,
    string Audience,
    string JwksUrl,
    bool RequireHttpsMetadata,
    int JwksTimeoutSeconds,
    int JwksRetryCount,
    int JwksRetryBaseDelayMilliseconds);
