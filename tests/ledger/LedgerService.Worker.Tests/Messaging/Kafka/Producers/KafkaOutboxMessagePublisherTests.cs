using System.Text;

using Confluent.Kafka;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Infrastructure.Observability;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Producers;
using LedgerService.Worker.Messaging.Kafka.Tracing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace LedgerService.Worker.Tests.Messaging.Kafka.Producers;

public sealed class KafkaOutboxMessagePublisherTests
{
    [Fact]
    public void ResolveDestination_should_use_topic_map_before_default_topic()
    {
        using var metrics = new OutboxMetrics("LedgerService.Worker.Tests.Outbox");
        using var sut = new KafkaOutboxMessagePublisher(
            Options.Create(new KafkaProducerOptions
            {
                BootstrapServers = "localhost:9092",
                DefaultTopic = "ledger.default",
                TopicMap = new Dictionary<string, string>
                {
                    ["LedgerEntryCreated"] = "ledger.ledgerentry.created"
                }
            }),
            Mock.Of<ILogger<KafkaOutboxMessagePublisher>>(),
            metrics);

        var mapped = sut.ResolveDestination(CreateOutboxMessage("LedgerEntryCreated"));
        var fallback = sut.ResolveDestination(CreateOutboxMessage("UnknownEvent"));

        Assert.Equal("ledger.ledgerentry.created", mapped);
        Assert.Equal("ledger.default", fallback);
    }

    [Fact]
    public async Task PublishAsync_should_publish_payload_key_and_contract_headers()
    {
        var producer = new Mock<IProducer<string, string>>(MockBehavior.Strict);
        Message<string, string>? publishedMessage = null;
        var outbox = CreateOutboxMessage("LedgerEntryCreated.v1");

        producer
            .Setup(x => x.ProduceAsync(
                "ledger.default",
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => publishedMessage = message)
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "ledger.default",
                Partition = new Partition(0),
                Offset = new Offset(1)
            });
        producer.Setup(x => x.Flush(It.IsAny<TimeSpan>())).Returns(0);
        producer.Setup(x => x.Dispose());

        using var metrics = new OutboxMetrics("LedgerService.Worker.Tests.Outbox.Contract");
        using var sut = new KafkaOutboxMessagePublisher(
            Options.Create(new KafkaProducerOptions
            {
                BootstrapServers = "localhost:9092",
                DefaultTopic = "ledger.default"
            }),
            Mock.Of<ILogger<KafkaOutboxMessagePublisher>>(),
            metrics,
            producer.Object);

        await sut.PublishAsync(outbox, CancellationToken.None);

        Assert.NotNull(publishedMessage);
        Assert.Equal(outbox.AggregateId.ToString("N"), publishedMessage!.Key);
        Assert.Equal(outbox.Payload, publishedMessage.Value);
        Assert.Equal(outbox.OccurredAt, publishedMessage.Timestamp.UtcDateTime);
        Assert.Equal(outbox.Id.ToString(), GetHeaderValue(publishedMessage.Headers, KafkaHeaderNames.EventId));
        Assert.Equal(outbox.EventType, GetHeaderValue(publishedMessage.Headers, KafkaHeaderNames.EventType));
        Assert.Equal(outbox.CorrelationId!.Value.ToString(), GetHeaderValue(publishedMessage.Headers, KafkaHeaderNames.CorrelationId));
        sut.Dispose();
        producer.VerifyAll();
    }

    private static string GetHeaderValue(Headers headers, string key)
        => Encoding.UTF8.GetString(headers.GetLastBytes(key));

    private static OutboxMessage CreateOutboxMessage(string eventType)
        => new(
            "LedgerEntry",
            Guid.NewGuid(),
            eventType,
            "{}",
            new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            null,
            null,
            null);
}
