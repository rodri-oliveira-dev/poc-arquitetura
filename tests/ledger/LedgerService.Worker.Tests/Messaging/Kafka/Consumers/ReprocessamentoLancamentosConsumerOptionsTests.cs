using Confluent.Kafka;

using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Consumers;

namespace LedgerService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class ReprocessamentoLancamentosConsumerOptionsTests
{
    [Theory]
    [InlineData("BootstrapServers", "BootstrapServers")]
    [InlineData("GroupId", "GroupId")]
    [InlineData("Topic", "Topic")]
    [InlineData("ConsumeErrorRetryDelay", "ConsumeErrorRetryDelay")]
    [InlineData("ProcessingErrorRetryDelay", "ProcessingErrorRetryDelay")]
    public void ValidateOptions_should_reject_invalid_required_values(
        string invalidField,
        string expectedMessage)
    {
        var options = CreateInvalidOptions(invalidField);

        var act = () => ReprocessamentoLancamentosConsumerService.ValidateOptions(options);

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape($"*{expectedMessage}*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Theory]
    [InlineData("Earliest", AutoOffsetReset.Earliest)]
    [InlineData("Latest", AutoOffsetReset.Latest)]
    [InlineData("unknown", AutoOffsetReset.Earliest)]
    public void ParseAutoOffsetReset_should_default_to_earliest_when_value_is_unknown(string value, AutoOffsetReset expected)
    {
        var result = ReprocessamentoLancamentosConsumerService.ParseAutoOffsetReset(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CreateConsumerConfig_should_map_options_and_security_fields()
    {
        var options = new ReprocessamentoLancamentosConsumerOptions
        {
            BootstrapServers = "kafka:9092",
            GroupId = "ledger-group",
            Topic = "ledger.lancamentos.reprocessamento.solicitado",
            ClientId = "ledger-client",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false,
            AutoOffsetReset = "Latest",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "Scram-Sha-256",
            SaslUsername = "user",
            SaslPassword = "secret",
            SslCaLocation = "/certs/ca.pem",
            ConsumeErrorRetryDelay = TimeSpan.FromMilliseconds(1),
            ProcessingErrorRetryDelay = TimeSpan.FromMilliseconds(1)
        };

        var config = ReprocessamentoLancamentosConsumerService.CreateConsumerConfig(options);

        Assert.Equal("kafka:9092", config.BootstrapServers);
        Assert.Equal("ledger-group", config.GroupId);
        Assert.Equal("ledger-client", config.ClientId);
        Assert.False(config.EnableAutoCommit);
        Assert.False(config.EnableAutoOffsetStore);
        Assert.False(config.AllowAutoCreateTopics);
        Assert.Equal(AutoOffsetReset.Latest, config.AutoOffsetReset);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha256, config.SaslMechanism);
        Assert.Equal("user", config.SaslUsername);
        Assert.Equal("secret", config.SaslPassword);
        Assert.Equal("/certs/ca.pem", config.SslCaLocation);
    }

    private static ReprocessamentoLancamentosConsumerOptions CreateInvalidOptions(string invalidField)
    {
        return invalidField switch
        {
            "BootstrapServers" => ValidOptions(bootstrapServers: ""),
            "GroupId" => ValidOptions(groupId: ""),
            "Topic" => ValidOptions(topic: ""),
            "ConsumeErrorRetryDelay" => ValidOptions(consumeErrorRetryDelay: TimeSpan.Zero),
            "ProcessingErrorRetryDelay" => ValidOptions(processingErrorRetryDelay: TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null)
        };
    }

    private static ReprocessamentoLancamentosConsumerOptions ValidOptions(
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
}
