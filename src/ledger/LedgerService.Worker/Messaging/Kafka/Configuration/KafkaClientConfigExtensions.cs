using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace LedgerService.Worker.Messaging.Kafka.Configuration;

public static class KafkaClientConfigExtensions
{
    public static void ApplySecurity(this ClientConfig config, KafkaProducerOptions options)
        => KafkaClientSecurity.ApplySecurity(config, options);

    public static bool IsPlaintext(KafkaProducerOptions options)
        => KafkaClientSecurity.IsPlaintext(options);

    public static void ApplySecurity(this ClientConfig config, ReprocessamentoLancamentosConsumerOptions options)
        => KafkaConsumerConfigFactory.ApplySecurity(config, options);

    public static bool IsPlaintext(ReprocessamentoLancamentosConsumerOptions options)
        => KafkaConsumerConfigFactory.IsPlaintext(options);
}
