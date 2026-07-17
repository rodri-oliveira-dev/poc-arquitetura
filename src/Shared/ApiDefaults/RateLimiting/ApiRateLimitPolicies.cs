namespace ApiDefaults.RateLimiting;

public static class ApiRateLimitPolicies
{
    public const string LegacyFixed = "fixed";
    public const string AuthenticatedRead = "authenticated-read";
    public const string AuthenticatedWrite = "authenticated-write";
    public const string Administrative = "administrative";
    public const string AnonymousWebhook = "anonymous-webhook";
}
