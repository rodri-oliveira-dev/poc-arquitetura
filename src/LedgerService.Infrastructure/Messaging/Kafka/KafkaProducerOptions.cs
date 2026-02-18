namespace LedgerService.Infrastructure.Messaging.Kafka;

public sealed class KafkaProducerOptions
{
    public const string SectionName = "Kafka:Producer";

    public string BootstrapServers { get; init; } = string.Empty;
    public string ClientId { get; init; } = "ledger-service";

    /// <summary>
    /// Kafka acks: 0, 1, all
    /// </summary>
    public string Acks { get; init; } = "all";

    public bool EnableIdempotence { get; init; } = true;

    /// <summary>
    /// Nome do tópico padrão (pode ser sobrescrito por EventType->TopicMap).
    /// </summary>
    public string DefaultTopic { get; init; } = "ledger-events";

    /// <summary>
    /// Mapa opcional por tipo de evento -> tópico.
    /// Ex: { "LedgerEntryCreated": "ledger.ledgerentry.created" }
    /// </summary>
    public Dictionary<string, string> TopicMap { get; init; } = new();

    public int MessageTimeoutMs { get; init; } = 30000;
}
