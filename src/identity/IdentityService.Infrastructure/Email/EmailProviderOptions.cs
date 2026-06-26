namespace IdentityService.Infrastructure.Email;

public sealed class EmailProviderOptions
{
    public const string SectionName = "Email";
    public const string Resend = nameof(Resend);
    public const string Mailpit = nameof(Mailpit);

    public string Provider
    {
        get;
        init;
    } = Resend;
}
