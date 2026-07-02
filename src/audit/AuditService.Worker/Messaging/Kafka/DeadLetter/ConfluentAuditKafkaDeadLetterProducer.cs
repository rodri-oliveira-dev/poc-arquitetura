using Confluent.Kafka;

namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal sealed class ConfluentAuditKafkaDeadLetterProducer(IProducer<string, string> inner) : IAuditKafkaDeadLetterProducer
{
    public Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        Message<string, string> message,
        CancellationToken cancellationToken)
        => inner.ProduceAsync(topic, message, cancellationToken);

    public void Flush(TimeSpan timeout)
        => inner.Flush(timeout);

    public void Dispose()
        => inner.Dispose();
}
