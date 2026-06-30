using IdentityService.Application.Common.DomainEvents;
using IdentityService.Domain.Users;

using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.DomainEvents;

internal sealed partial class LogUserRegisteredDomainEventHandler(
    ILogger<LogUserRegisteredDomainEventHandler> logger) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        UserRegistered(logger, domainEvent.UserId.Value, domainEvent.MerchantId.Value);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Identity user registered. UserId={UserId} MerchantId={MerchantId}")]
    private static partial void UserRegistered(ILogger logger, Guid userId, string merchantId);
}
