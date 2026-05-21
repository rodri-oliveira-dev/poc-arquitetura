using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Messaging.Kafka.Tracing;
using BalanceService.Worker.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BalanceService.Worker.Tests.Tests;

public sealed class WorkerCoverageCompositionTests
{
    [Theory]
    [InlineData("BootstrapServers", "BootstrapServers")]
    [InlineData("GroupId", "GroupId")]
    [InlineData("Topics", "Topics")]
    [InlineData("DeadLetterTopic", "DeadLetterTopic")]
    [InlineData("InvalidMessageRetryDelay", "InvalidMessageRetryDelay")]
    [InlineData("ConsumeErrorRetryDelay", "ConsumeErrorRetryDelay")]
    [InlineData("ProcessingErrorRetryDelay", "ProcessingErrorRetryDelay")]
    public void LedgerEventsConsumer_should_validate_options_before_opening_kafka(
        string invalidField,
        string expectedMessage)
    {
        var options = CreateInvalidConsumerOptions(invalidField);

        var act = () => LedgerEventsConsumer.ValidateOptions(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    [Theory]
    [InlineData("Earliest", Confluent.Kafka.AutoOffsetReset.Earliest)]
    [InlineData("Latest", Confluent.Kafka.AutoOffsetReset.Latest)]
    [InlineData("unknown", Confluent.Kafka.AutoOffsetReset.Earliest)]
    public void LedgerEventsConsumer_should_parse_auto_offset_reset(string value, Confluent.Kafka.AutoOffsetReset expected)
    {
        var result = LedgerEventsConsumer.ParseAutoOffsetReset(value);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task KafkaDeadLetterProducer_should_validate_dead_letter_topic_before_producing()
    {
        using var metrics = new KafkaMessagingMetrics("BalanceService.Worker.Tests.Dlq");
        using var sut = new KafkaDeadLetterProducer(
            Options.Create(ValidConsumerOptions(deadLetterTopic: "")),
            metrics,
            Mock.Of<ILogger<KafkaDeadLetterProducer>>());

        var message = new DeadLetterMessage(
            "{}",
            "ledger.ledgerentry.created",
            0,
            10,
            new Dictionary<string, string>(),
            "Missing required Kafka header event_id.",
            nameof(InvalidOperationException),
            DateTimeOffset.UtcNow);

        var act = () => sut.ProduceAsync(message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DeadLetterTopic*");
    }

    [Theory]
    [InlineData("Deserialization failed.", "deserialization_failed")]
    [InlineData("Non-recoverable processing failure.", "non_recoverable_processing_failure")]
    [InlineData("Missing required Kafka header event_id.", "validation_failed")]
    [InlineData("Unsupported Kafka event_type LedgerEntryCreated.v2.", "validation_failed")]
    [InlineData("Message payload invalid.", "validation_failed")]
    [InlineData("Transient failure.", "unknown")]
    public void KafkaDeadLetterProducer_should_classify_dlq_reasons(string reason, string expected)
    {
        KafkaDeadLetterProducer.ClassifyReason(reason).Should().Be(expected);
    }

    [Fact]
    public void KafkaDeadLetterProducer_should_resolve_event_type_from_headers()
    {
        var eventType = KafkaDeadLetterProducer.ResolveEventType(new Dictionary<string, string>
        {
            [KafkaHeaderNames.EventType] = "LedgerEntryCreated.v1"
        });

        var unknown = KafkaDeadLetterProducer.ResolveEventType(new Dictionary<string, string>());

        eventType.Should().Be("LedgerEntryCreated.v1");
        unknown.Should().Be("unknown");
    }

    private static KafkaConsumerOptions CreateInvalidConsumerOptions(string invalidField)
    {
        return invalidField switch
        {
            "BootstrapServers" => ValidConsumerOptions(bootstrapServers: ""),
            "GroupId" => ValidConsumerOptions(groupId: ""),
            "Topics" => ValidConsumerOptions(topics: new List<string>()),
            "DeadLetterTopic" => ValidConsumerOptions(deadLetterTopic: ""),
            "InvalidMessageRetryDelay" => ValidConsumerOptions(invalidMessageRetryDelay: TimeSpan.Zero),
            "ConsumeErrorRetryDelay" => ValidConsumerOptions(consumeErrorRetryDelay: TimeSpan.Zero),
            "ProcessingErrorRetryDelay" => ValidConsumerOptions(processingErrorRetryDelay: TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null)
        };
    }

    private static KafkaConsumerOptions ValidConsumerOptions(
        string bootstrapServers = "localhost:9092",
        string groupId = "balance-service",
        List<string>? topics = null,
        string deadLetterTopic = "ledger.ledgerentry.created.dlq",
        TimeSpan? invalidMessageRetryDelay = null,
        TimeSpan? consumeErrorRetryDelay = null,
        TimeSpan? processingErrorRetryDelay = null)
        => new()
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            Topics = topics ?? new List<string> { "ledger.ledgerentry.created" },
            DeadLetterTopic = deadLetterTopic,
            InvalidMessageRetryDelay = invalidMessageRetryDelay ?? TimeSpan.FromMilliseconds(1),
            ConsumeErrorRetryDelay = consumeErrorRetryDelay ?? TimeSpan.FromMilliseconds(1),
            ProcessingErrorRetryDelay = processingErrorRetryDelay ?? TimeSpan.FromMilliseconds(1)
        };
}
