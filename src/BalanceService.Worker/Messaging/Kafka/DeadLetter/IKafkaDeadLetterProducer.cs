namespace BalanceService.Worker.Messaging.Kafka.DeadLetter;

public interface IKafkaDeadLetterProducer
{
    Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}
