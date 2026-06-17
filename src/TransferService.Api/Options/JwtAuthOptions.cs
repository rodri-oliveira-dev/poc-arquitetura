namespace TransferService.Api.Options;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string JwksUrl { get; init; } = string.Empty;

    public bool RequireHttpsMetadata { get; init; } = true;

    public int JwksTimeoutSeconds { get; init; } = 5;

    public int JwksRetryCount { get; init; } = 2;

    public int JwksRetryBaseDelayMilliseconds { get; init; } = 200;
}
