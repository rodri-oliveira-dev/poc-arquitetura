using System.Text.Json;

using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Messaging.Kafka.DeadLetter;
using AuditService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Worker.Tests.Messaging.Kafka;

public sealed class KafkaAuditRecordDeadLetterPublisherTests
{
    [Fact]
    public async Task PublishAsync_should_publish_hash_and_transport_metadata_without_original_payload()
    {
        using var producer = new RecordingProducer();
        using var metrics = new AuditWorkerMetrics($"AuditService.Worker.Tests.{Guid.NewGuid():N}");
        using var publisher = new KafkaAuditRecordDeadLetterPublisher(
            Microsoft.Extensions.Options.Options.Create(new AuditRecordRequestedConsumerOptions
            {
                BootstrapServers = "localhost:9092",
                DeadLetterTopic = "audit.record.requested.dlq"
            }),
            metrics,
            new RecordingProducerFactory(producer),
            NullLogger<KafkaAuditRecordDeadLetterPublisher>.Instance);

        await publisher.PublishAsync(
            new AuditRecordDeadLetterMessage(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                "audit.record.requested",
                0,
                42,
                "Contrato invalido.",
                "invalid_contract",
                DateTimeOffset.Parse("2026-07-01T10:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
                "abc123"),
            TestContext.Current.CancellationToken);

        Assert.Equal("audit.record.requested.dlq", producer.Topic);
        Assert.NotNull(producer.Message);
        Assert.Equal("audit.record.requested:0:42", producer.Message.Key);
        Assert.Contains(producer.Message.Headers, header => header.Key == "event_id");
        Assert.Contains(producer.Message.Headers, header => header.Key == "correlation_id");
        Assert.Contains(producer.Message.Headers, header => header.Key == "original_offset");

        using JsonDocument document = JsonDocument.Parse(producer.Message.Value);
        Assert.Equal("abc123", document.RootElement.GetProperty("payloadSha256").GetString());
        Assert.False(producer.Message.Value.Contains("payload original", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingProducerFactory(RecordingProducer producer) : IAuditKafkaDeadLetterProducerFactory
    {
        public IAuditKafkaDeadLetterProducer Create()
            => producer;
    }

    private sealed class RecordingProducer : IAuditKafkaDeadLetterProducer
    {
        public string? Topic
        {
            get; private set;
        }
        public Message<string, string>? Message
        {
            get; private set;
        }

        public Task<DeliveryResult<string, string>> ProduceAsync(
            string topic,
            Message<string, string> message,
            CancellationToken cancellationToken)
        {
            Topic = topic;
            Message = message;
            return Task.FromResult(new DeliveryResult<string, string>
            {
                Topic = topic,
                Partition = new Partition(0),
                Offset = new Offset(1),
                Message = message
            });
        }

        public void Flush(TimeSpan timeout)
        {
        }

        public void Dispose()
        {
        }
    }
}
