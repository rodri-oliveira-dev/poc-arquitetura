using IdentityService.Domain.Common;

namespace IdentityService.Application.Common.DomainEvents;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
