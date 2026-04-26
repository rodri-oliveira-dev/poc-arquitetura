namespace LedgerService.Api.Options;

public sealed class ApiLimitsOptions
{
    public const string SectionName = "ApiLimits";

    public long MaxRequestBodySizeBytes { get; init; } = 1_048_576;

    public int RateLimitPermitLimit { get; init; } = 100;

    public int RateLimitWindowSeconds { get; init; } = 60;

    public int RateLimitQueueLimit { get; init; } = 10;
}
