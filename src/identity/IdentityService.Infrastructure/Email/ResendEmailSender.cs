using IdentityService.Application.Users.Ports;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Resend;

using ApplicationEmailMessage = IdentityService.Application.Users.Ports.EmailMessage;
using ResendEmailMessage = Resend.EmailMessage;

namespace IdentityService.Infrastructure.Email;

public sealed partial class ResendEmailSender(
    IResendClientFactory clientFactory,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(ApplicationEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        Validate(currentOptions);

        var resendMessage = new ResendEmailMessage
        {
            From = BuildSender(currentOptions),
            To = [],
            Subject = message.Subject,
            HtmlBody = message.HtmlBody
        };
        resendMessage.To.Add(message.ToAddress);

        if (!string.IsNullOrWhiteSpace(currentOptions.ReplyTo))
        {
            resendMessage.ReplyTo = [];
            resendMessage.ReplyTo.Add(currentOptions.ReplyTo);
        }

        try
        {
            var response = await clientFactory
                .CreateClient()
                .EmailSendAsync(resendMessage, cancellationToken);

            if (!response.Success)
            {
                if (response.Exception is not null)
                    throw response.Exception;

                throw new InvalidOperationException("Resend retornou falha sem detalhes.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ResendEmailFailed(logger, exception, message.ToAddress, message.Subject);
            throw;
        }
    }

    private static EmailAddress BuildSender(ResendOptions options)
    {
        var from = Required(options.From, "Resend:From");

        return string.IsNullOrWhiteSpace(options.FromName)
            ? EmailAddress.Parse(from)
            : EmailAddress.Parse($"{options.FromName} <{from}>");
    }

    private static void Validate(ResendOptions options)
    {
        _ = Required(options.ApiKey, "Resend:ApiKey");
        _ = Required(options.From, "Resend:From");
    }

    private static string Required(string? value, string configurationKey)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} nao foi configurado.")
            : value;

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Resend email failed. To={ToAddress} Subject={Subject}")]
    private static partial void ResendEmailFailed(
        ILogger logger,
        Exception exception,
        string toAddress,
        string subject);
}
