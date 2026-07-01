using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace BalanceService.Worker.Tests.Messaging.Kafka.DeadLetter;

public sealed class KafkaDeadLetterPublisherTests
{
    [Fact]
    public async Task PublishAsync_should_validate_dead_letter_topic_before_publishing()
    {
        using var metrics = new MessagingMetrics("BalanceService.Worker.Tests.Dlq");
        using var sut = new KafkaDeadLetterPublisher(
            Options.Create(ValidConsumerOptions(deadLetterTopic: "")),
            metrics,
            Mock.Of<ILogger<KafkaDeadLetterPublisher>>());

        var message = new DeadLetterMessage(
            "{}",
            "ledger.ledgerentry.created",
            "kafka",
            "unknown",
            "Missing required message attribute event_id.",
            nameof(InvalidOperationException),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                ["topic"] = "ledger.ledgerentry.created",
                ["partition"] = "0",
                ["offset"] = "10"
            });

        var act = () => sut.PublishAsync(message, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*DeadLetterTopic*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Theory]
    [InlineData("Deserialization failed.", "deserialization_failed")]
    [InlineData("Non-recoverable processing failure.", "non_recoverable_processing_failure")]
    [InlineData("Missing required message attribute event_id.", "validation_failed")]
    [InlineData("Unsupported message event_type LedgerEntryCreated.v2.", "validation_failed")]
    [InlineData("Message payload invalid.", "validation_failed")]
    [InlineData("Transient failure.", "unknown")]
    public void ClassifyReason_should_map_known_failure_reasons(string reason, string expected)
    {
        Assert.Equal(expected, KafkaDeadLetterPublisher.ClassifyReason(reason));
    }

    [Fact]
    public void ResolveEventType_should_use_attribute_or_unknown_fallback()
    {
        var eventType = KafkaDeadLetterPublisher.ResolveEventType(new Dictionary<string, string>
        {
            [MessageAttributeNames.EventType] = "LedgerEntryCreated.v1"
        });

        var unknown = KafkaDeadLetterPublisher.ResolveEventType(new Dictionary<string, string>());

        Assert.Equal("LedgerEntryCreated.v1", eventType);
        Assert.Equal("unknown", unknown);
    }

    private static KafkaConsumerOptions ValidConsumerOptions(string deadLetterTopic = "ledger.ledgerentry.created.dlq")
        => new()
        {
            BootstrapServers = "localhost:9092",
            GroupId = "balance-service",
            Topics = new List<string> { "ledger.ledgerentry.created" },
            DeadLetterTopic = deadLetterTopic,
            InvalidMessageRetryDelay = TimeSpan.FromMilliseconds(1),
            ConsumeErrorRetryDelay = TimeSpan.FromMilliseconds(1),
            ProcessingErrorRetryDelay = TimeSpan.FromMilliseconds(1)
        };
}
