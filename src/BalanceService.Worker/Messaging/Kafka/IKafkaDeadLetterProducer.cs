namespace BalanceService.Worker.Messaging.Kafka;

public interface IKafkaDeadLetterProducer
{
    Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}
