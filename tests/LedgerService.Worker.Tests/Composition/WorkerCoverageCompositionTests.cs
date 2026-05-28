using Confluent.Kafka;
using LedgerService.Application.Outbox.Retry;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Observability;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Producers;
using LedgerService.Worker.Outbox;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Kafka.Processors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Worker.Tests.Composition;

public sealed class WorkerCoverageCompositionTests
{
    [Fact]
    public void KafkaOutboxMessagePublisher_should_resolve_mapped_destination_before_default_topic()
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
    public void OutboxKafkaPublisherService_should_compute_exponential_retry_with_bounded_jitter()
    {
        var now = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var sut = new ExponentialBackoffRetryStrategy(new FixedJitterProvider(TimeSpan.FromMilliseconds(100)));

        var nextAttempt = sut.CalculateNextRetry(now, retryCount: 2, TimeSpan.FromSeconds(2));
        Assert.True(nextAttempt >= now.AddSeconds(8));
        Assert.True(nextAttempt < now.AddSeconds(8).AddMilliseconds(250));
    }

    [Fact]
    public void OutboxKafkaPublisherService_should_be_constructed_with_worker_dependencies()
    {
        using var sut = new OutboxKafkaPublisherService(
            Mock.Of<IServiceProvider>(),
            Options.Create(new OutboxPublisherOptions()),
            Mock.Of<IRetryStrategy>(),
            Mock.Of<ILogger<OutboxKafkaPublisherService>>());
        Assert.NotNull(sut);
    }

    [Theory]
    [InlineData("BootstrapServers", "BootstrapServers")]
    [InlineData("GroupId", "GroupId")]
    [InlineData("Topic", "Topic")]
    [InlineData("ConsumeErrorRetryDelay", "ConsumeErrorRetryDelay")]
    [InlineData("ProcessingErrorRetryDelay", "ProcessingErrorRetryDelay")]
    public void ReprocessamentoConsumer_should_validate_options_before_opening_kafka(
        string invalidField,
        string expectedMessage)
    {
        var options = CreateInvalidReprocessamentoOptions(invalidField);

        var act = () => ReprocessamentoLancamentosConsumerService.ValidateOptions(options);
        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape($"*{expectedMessage}*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Theory]
    [InlineData("Earliest", AutoOffsetReset.Earliest)]
    [InlineData("Latest", AutoOffsetReset.Latest)]
    [InlineData("unknown", AutoOffsetReset.Earliest)]
    public void ReprocessamentoConsumer_should_parse_auto_offset_reset(string value, AutoOffsetReset expected)
    {
        var result = ReprocessamentoLancamentosConsumerService.ParseAutoOffsetReset(value);
        Assert.Equal(expected, result);
    }

    private static ReprocessamentoLancamentosConsumerOptions CreateInvalidReprocessamentoOptions(string invalidField)
    {
        return invalidField switch
        {
            "BootstrapServers" => ValidReprocessamentoOptions(bootstrapServers: ""),
            "GroupId" => ValidReprocessamentoOptions(groupId: ""),
            "Topic" => ValidReprocessamentoOptions(topic: ""),
            "ConsumeErrorRetryDelay" => ValidReprocessamentoOptions(consumeErrorRetryDelay: TimeSpan.Zero),
            "ProcessingErrorRetryDelay" => ValidReprocessamentoOptions(processingErrorRetryDelay: TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null)
        };
    }

    private static ReprocessamentoLancamentosConsumerOptions ValidReprocessamentoOptions(
        string bootstrapServers = "localhost:9092",
        string groupId = "ledger-service",
        string topic = "ledger.reprocessamentos",
        TimeSpan? consumeErrorRetryDelay = null,
        TimeSpan? processingErrorRetryDelay = null)
        => new()
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            Topic = topic,
            ConsumeErrorRetryDelay = consumeErrorRetryDelay ?? TimeSpan.FromMilliseconds(1),
            ProcessingErrorRetryDelay = processingErrorRetryDelay ?? TimeSpan.FromMilliseconds(1)
        };

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

    private sealed class FixedJitterProvider : IJitterProvider
    {
        private readonly TimeSpan _jitter;

        public FixedJitterProvider(TimeSpan jitter) => _jitter = jitter;

        public TimeSpan NextJitter() => _jitter;
    }
}
