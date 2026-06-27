namespace IdentityService.Infrastructure.Email;

public sealed class WelcomeEmailOptions
{
    public const string SectionName = "Email";

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
