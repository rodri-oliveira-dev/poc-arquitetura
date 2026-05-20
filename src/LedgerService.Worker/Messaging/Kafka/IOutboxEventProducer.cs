using LedgerService.Domain.Entities;

namespace LedgerService.Worker.Messaging.Kafka;

public interface IOutboxEventProducer
{
    string ResolveTopic(OutboxMessage message);

    Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken);
}
