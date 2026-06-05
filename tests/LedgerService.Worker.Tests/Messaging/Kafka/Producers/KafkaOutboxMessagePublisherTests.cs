using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Observability;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Producers;
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

    private static OutboxMessage CreateOutboxMessage(string eventType)
        => new(
            "LedgerEntry",
            Guid.NewGuid(),
            eventType,
            "{}",
            DateTime.UtcNow,
            Guid.NewGuid(),
            null,
            null,
            null);
}
