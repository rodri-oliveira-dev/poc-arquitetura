namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka:Consumer";

    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string ClientId { get; init; } = "balance-service";

    public string AutoOffsetReset { get; init; } = "Earliest";
    public bool EnableAutoCommit { get; init; } = false;
    public bool EnableAutoOffsetStore { get; init; } = false;
    public bool AllowAutoCreateTopics { get; init; } = false;

    public List<string> Topics { get; init; } = new();

    // TODO: se o projeto passar a usar autenticação (SASL/SSL), incluir campos aqui e mapear no ConsumerConfig.
}
