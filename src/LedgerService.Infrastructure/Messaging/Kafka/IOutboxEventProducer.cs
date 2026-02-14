using LedgerService.Domain.Entities;

namespace LedgerService.Infrastructure.Messaging.Kafka;

public interface IOutboxEventProducer
{
    Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken);
}
