using Confluent.Kafka;

namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal interface IAuditKafkaDeadLetterProducer : IDisposable
{
    Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        Message<string, string> message,
        CancellationToken cancellationToken);

    void Flush(TimeSpan timeout);
}
