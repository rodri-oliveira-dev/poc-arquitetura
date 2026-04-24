namespace BalanceService.Infrastructure.Messaging.Kafka;

public interface IKafkaDeadLetterProducer
{
    Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}
