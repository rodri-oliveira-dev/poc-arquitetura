namespace IdentityService.Infrastructure.Email;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string? ApiKey
    {
        get;
        set;
    }

    public string? From
    {
        get;
        set;
    }

    public string? FromName
    {
        get;
        set;
    }

    public string? ReplyTo
    {
        get;
        set;
    }
}
