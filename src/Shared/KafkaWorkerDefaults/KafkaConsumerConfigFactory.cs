using Confluent.Kafka;

namespace PocArquitetura.KafkaWorkerDefaults;

public static class KafkaConsumerConfigFactory
{
    public static ConsumerConfig Create(IKafkaConsumerConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            ClientId = options.ClientId,
            EnableAutoCommit = options.EnableAutoCommit,
            EnableAutoOffsetStore = options.EnableAutoOffsetStore,
            AllowAutoCreateTopics = options.AllowAutoCreateTopics,
            AutoOffsetReset = KafkaOffsetResetParser.Parse(options.AutoOffsetReset)
        };

        KafkaClientSecurity.ApplySecurity(config, options);
        return config;
    }

    public static void ApplySecurity(ClientConfig config, IKafkaClientSecurityOptions options)
        => KafkaClientSecurity.ApplySecurity(config, options);

    public static bool IsPlaintext(IKafkaClientSecurityOptions options)
        => KafkaClientSecurity.IsPlaintext(options);
}
