using LedgerService.Application.Abstractions.Messaging;

namespace LedgerService.Worker.Messaging.Abstractions;

public interface IOutboxMessagePublisher
{
    string ResolveDestination(OutboxMessage message);

    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
