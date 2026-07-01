using Confluent.Kafka;

namespace AuditService.Worker.Messaging.Kafka;

internal interface IAuditKafkaConsumer : IDisposable
{
    void Subscribe(string topic);

    ConsumeResult<string, string>? Consume(CancellationToken cancellationToken);

    void Commit(ConsumeResult<string, string> result);

    void Close();
}
