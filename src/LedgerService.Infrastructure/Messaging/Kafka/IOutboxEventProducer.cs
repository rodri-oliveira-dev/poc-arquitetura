using LedgerService.Domain.Entities;

namespace LedgerService.Infrastructure.Messaging.Kafka;

public interface IOutboxEventProducer
{
    string ResolveTopic(OutboxMessage message);

    Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken);
}
