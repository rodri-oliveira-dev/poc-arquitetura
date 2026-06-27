using System.Net.Mail;
using System.Text;

using IdentityService.Application.Users.Ports;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ApplicationEmailMessage = IdentityService.Application.Users.Ports.EmailMessage;

namespace IdentityService.Infrastructure.Email;

public sealed partial class MailpitEmailSender(
    IOptions<MailpitOptions> options,
    ILogger<MailpitEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(ApplicationEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        Validate(currentOptions);

        using var smtpMessage = new MailMessage
        {
            From = new MailAddress(
                Required(currentOptions.From, "Mailpit:From"),
                currentOptions.FromName,
                Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        smtpMessage.To.Add(new MailAddress(message.ToAddress, message.ToName, Encoding.UTF8));

        using var client = new SmtpClient(
            Required(currentOptions.Host, "Mailpit:Host"),
            currentOptions.Port)
        {
            EnableSsl = currentOptions.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        try
        {
            await client.SendMailAsync(smtpMessage, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MailpitEmailFailed(logger, exception, message.ToAddress, message.Subject);
            throw;
        }
    }

    private static void Validate(MailpitOptions options)
    {
        _ = Required(options.Host, "Mailpit:Host");
        _ = Required(options.From, "Mailpit:From");

        if (options.Port <= 0)
            throw new InvalidOperationException("Mailpit:Port deve ser maior que zero.");
    }

    private static string Required(string? value, string configurationKey)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} nao foi configurado.")
            : value;

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Mailpit email failed. To={ToAddress} Subject={Subject}")]
    private static partial void MailpitEmailFailed(
        ILogger logger,
        Exception exception,
        string toAddress,
        string subject);
}
