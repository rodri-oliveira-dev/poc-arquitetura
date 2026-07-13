using PocArquitetura.KafkaWorkerDefaults;

namespace AuditService.Worker.Messaging.Kafka.Configuration;

public sealed class AuditRecordRequestedConsumerOptions : IKafkaConsumerConfigOptions
{
    public const string SectionName = "Kafka:AuditRecordRequestedConsumer";

    public bool Enabled
    {
        get; init;
    }
    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = "audit-record-requested-consumer";
    public string ClientId { get; init; } = "audit-record-requested-consumer-1";
    public string SecurityProtocol { get; init; } = "Plaintext";
    public string SaslMechanism { get; init; } = string.Empty;
    public string SaslUsername { get; init; } = string.Empty;
    public string SaslPassword { get; init; } = string.Empty;
    public string SslCaLocation { get; init; } = string.Empty;
    public string Topic { get; init; } = "audit.record.requested";
    public string AutoOffsetReset { get; init; } = "Earliest";
    public bool EnableAutoCommit
    {
        get; init;
    }
    public bool EnableAutoOffsetStore
    {
        get; init;
    }
    public bool AllowAutoCreateTopics
    {
        get; init;
    }
    public string DeadLetterTopic { get; init; } = "audit.record.requested.dlq";
    public int DeadLetterMessageTimeoutMs { get; init; } = 30000;
    public int MaxProcessingAttempts { get; init; } = 3;
    public TimeSpan ProcessingRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan ConsumeErrorRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan ProcessingErrorRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}
