namespace ApiDefaults.Options;

public class ApiDefaultsOptions
{
    public const string SectionName = "ApiLimits";

    public long MaxRequestBodySizeBytes { get; init; } = 1_048_576;

    public int RateLimitPermitLimit { get; init; } = 100;

    public int RateLimitWindowSeconds { get; init; } = 60;

    public int RateLimitQueueLimit { get; init; } = 10;

    public RateLimitPolicyOptions AuthenticatedReadRateLimit { get; init; } = new();

    public RateLimitPolicyOptions AuthenticatedWriteRateLimit { get; init; } = new();

    public RateLimitPolicyOptions AdministrativeRateLimit { get; init; } = new();

    public RateLimitPolicyOptions AnonymousWebhookRateLimit { get; init; } = new();

    public RateLimitPolicyOptions SwaggerRateLimit { get; init; } = new();
}
