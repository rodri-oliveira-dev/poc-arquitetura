using System.Net;
using System.Net.Mail;

using IdentityService.Application.Users.Ports;

using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<SmtpEmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        Validate(currentOptions);

        using var mail = new MailMessage
        {
            From = new MailAddress(currentOptions.FromAddress!, currentOptions.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(message.ToAddress, message.ToName));

        using var client = new SmtpClient(currentOptions.Host!, currentOptions.Port)
        {
            EnableSsl = currentOptions.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            client.Credentials = new NetworkCredential(currentOptions.Username, currentOptions.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }

    private static void Validate(SmtpEmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new InvalidOperationException("Email:Smtp:Host nao foi configurado.");

        if (options.Port <= 0)
            throw new InvalidOperationException("Email:Smtp:Port deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(options.FromAddress))
            throw new InvalidOperationException("Email:Smtp:FromAddress nao foi configurado.");
    }
}
