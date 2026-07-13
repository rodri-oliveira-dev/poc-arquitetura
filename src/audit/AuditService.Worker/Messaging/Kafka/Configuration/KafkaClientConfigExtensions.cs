using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace AuditService.Worker.Messaging.Kafka.Configuration;

internal static class KafkaClientConfigExtensions
{
    public static void ApplySecurity(this ClientConfig config, AuditRecordRequestedConsumerOptions options)
        => KafkaClientSecurity.ApplySecurity(config, options);

    public static bool IsPlaintext(AuditRecordRequestedConsumerOptions options)
        => KafkaClientSecurity.IsPlaintext(options);
}
