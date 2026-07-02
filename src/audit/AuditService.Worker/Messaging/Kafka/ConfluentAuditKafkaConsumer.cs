using Confluent.Kafka;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed class ConfluentAuditKafkaConsumer(IConsumer<string, string> inner) : IAuditKafkaConsumer
{
    public void Subscribe(string topic)
        => inner.Subscribe(topic);

    public ConsumeResult<string, string>? Consume(CancellationToken cancellationToken)
        => inner.Consume(cancellationToken);

    public void Commit(ConsumeResult<string, string> result)
        => inner.Commit(result);

    public void Close()
        => inner.Close();

    public void Dispose()
        => inner.Dispose();
}
