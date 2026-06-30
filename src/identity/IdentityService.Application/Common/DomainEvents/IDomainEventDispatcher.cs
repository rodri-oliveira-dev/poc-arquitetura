using IdentityService.Domain.Common;

namespace IdentityService.Application.Common.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
