using IdentityService.Application.Common.DomainEvents;
using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Email;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.DomainEvents;

public sealed partial class SendWelcomeEmailOnUserRegisteredDomainEventHandler(
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<WelcomeEmailOptions> options,
    ILogger<SendWelcomeEmailOnUserRegisteredDomainEventHandler> logger) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    private const string Subject = "Bem-vindo";

    public async Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        try
        {
            var currentOptions = options.Value;
            var htmlBody = await templateRenderer.RenderAsync(
                Required(currentOptions.TemplatePath, "Email:TemplatePath"),
                new Dictionary<string, string>
                {
                    ["UserName"] = domainEvent.Username.Value,
                    ["MerchantId"] = domainEvent.MerchantId.Value,
                    ["AuthenticationLink"] = Required(currentOptions.AuthenticationUrl, "Email:AuthenticationUrl")
                },
                cancellationToken);

            await emailSender.SendAsync(
                new EmailMessage(
                    domainEvent.Email.Value,
                    domainEvent.Username.Value,
                    Subject,
                    htmlBody),
                cancellationToken);
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            WelcomeEmailFailed(logger, exception, domainEvent.UserId.Value, domainEvent.MerchantId.Value);
        }
    }

    private static string Required(string? value, string configurationKey)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{configurationKey} nao foi configurado.")
            : value;

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Welcome email failed for identity user. UserId={UserId} MerchantId={MerchantId}")]
    private static partial void WelcomeEmailFailed(
        ILogger logger,
        Exception exception,
        Guid userId,
        string merchantId);
}
