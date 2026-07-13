using LedgerService.Worker.Messaging.Processors;

using PocArquitetura.KafkaWorkerDefaults;

namespace LedgerService.Worker.Messaging.Kafka.Configuration;

public sealed class ReprocessamentoLancamentosConsumerOptions : IKafkaConsumerConfigOptions
{
    public const string SectionName = "Reprocessamentos:Consumer";

    public bool Enabled { get; init; } = true;
    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = "ledger-reprocessamento-consumer";
    public string ClientId { get; init; } = "ledger-reprocessamento-consumer-1";
    public string SecurityProtocol { get; init; } = "Plaintext";
    public string SaslMechanism { get; init; } = string.Empty;
    public string SaslUsername { get; init; } = string.Empty;
    public string SaslPassword { get; init; } = string.Empty;
    public string SslCaLocation { get; init; } = string.Empty;
    public string Topic { get; init; } = ReprocessamentoLancamentosMessageProcessor.SourceName;
    public string AutoOffsetReset { get; init; } = "Earliest";
    public bool EnableAutoCommit { get; init; } = false;
    public bool EnableAutoOffsetStore { get; init; } = false;
    public bool AllowAutoCreateTopics { get; init; } = false;
    public TimeSpan ConsumeErrorRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan ProcessingErrorRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}
