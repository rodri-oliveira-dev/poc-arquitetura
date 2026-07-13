using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace BalanceService.Worker.Messaging.Kafka.Configuration;

public static class KafkaClientConfigExtensions
{
    public static void ApplySecurity(this ClientConfig config, KafkaConsumerOptions options)
        => KafkaConsumerConfigFactory.ApplySecurity(config, options);

    public static bool IsPlaintext(KafkaConsumerOptions options)
        => KafkaConsumerConfigFactory.IsPlaintext(options);
}
