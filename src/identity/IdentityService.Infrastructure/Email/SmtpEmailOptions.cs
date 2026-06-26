namespace IdentityService.Infrastructure.Email;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host
    {
        get;
        set;
    }

    public int Port
    {
        get;
        set;
    } = 25;

    public bool EnableSsl
    {
        get;
        set;
    }

    public string? Username
    {
        get;
        set;
    }

    public string? Password
    {
        get;
        set;
    }

    public string? FromName
    {
        get;
        set;
    }

    public string? FromAddress
    {
        get;
        set;
    }

    public string? TemplatePath
    {
        get;
        set;
    }

    public string? AuthenticationUrl
    {
        get;
        set;
    }
}
