using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using Confluent.Kafka;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class LedgerEventsConsumerOptionsTests
{
    [Theory]
    [InlineData("BootstrapServers", "BootstrapServers")]
    [InlineData("GroupId", "GroupId")]
    [InlineData("Topics", "Topics")]
    [InlineData("DeadLetterTopic", "DeadLetterTopic")]
    [InlineData("InvalidMessageRetryDelay", "InvalidMessageRetryDelay")]
    [InlineData("ConsumeErrorRetryDelay", "ConsumeErrorRetryDelay")]
    [InlineData("ProcessingErrorRetryDelay", "ProcessingErrorRetryDelay")]
    public void ValidateOptions_should_reject_invalid_required_values(
        string invalidField,
        string expectedMessage)
    {
        var options = CreateInvalidOptions(invalidField);

        var act = () => LedgerEventsConsumer.ValidateOptions(options);

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape($"*{expectedMessage}*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Theory]
    [InlineData("Earliest", AutoOffsetReset.Earliest)]
    [InlineData("Latest", AutoOffsetReset.Latest)]
    [InlineData("unknown", AutoOffsetReset.Earliest)]
    public void ParseAutoOffsetReset_should_default_to_earliest_when_value_is_unknown(string value, AutoOffsetReset expected)
    {
        var result = LedgerEventsConsumer.ParseAutoOffsetReset(value);

        Assert.Equal(expected, result);
    }

    private static KafkaConsumerOptions CreateInvalidOptions(string invalidField)
    {
        return invalidField switch
        {
            "BootstrapServers" => ValidOptions(bootstrapServers: ""),
            "GroupId" => ValidOptions(groupId: ""),
            "Topics" => ValidOptions(topics: new List<string>()),
            "DeadLetterTopic" => ValidOptions(deadLetterTopic: ""),
            "InvalidMessageRetryDelay" => ValidOptions(invalidMessageRetryDelay: TimeSpan.Zero),
            "ConsumeErrorRetryDelay" => ValidOptions(consumeErrorRetryDelay: TimeSpan.Zero),
            "ProcessingErrorRetryDelay" => ValidOptions(processingErrorRetryDelay: TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null)
        };
    }

    private static KafkaConsumerOptions ValidOptions(
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
