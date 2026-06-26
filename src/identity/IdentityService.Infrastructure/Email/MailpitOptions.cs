namespace IdentityService.Infrastructure.Email;

public sealed class MailpitOptions
{
    public const string SectionName = "Mailpit";

    public string Host
    {
        get;
        init;
    } = "localhost";

    public int Port
    {
        get;
        init;
    } = 1025;

    public bool EnableSsl
    {
        get;
        init;
    }

    public string From
    {
        get;
        init;
    } = "noreply@poc-arquitetura.local";

    public string FromName
    {
        get;
        init;
    } = "POC Arquitetura";
}
