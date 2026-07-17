namespace ApiDefaults.Options;

public sealed class RateLimitPolicyOptions
{
    public int? PermitLimit
    {
        get;
        init;
    }

    public int? WindowSeconds
    {
        get;
        init;
    }

    public int? QueueLimit
    {
        get;
        init;
    }
}
