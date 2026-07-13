using AuditService.Worker.Messaging.Kafka.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

using PocArquitetura.KafkaWorkerDefaults;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class ConfluentAuditKafkaConsumerFactory(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    ILogger<ConfluentAuditKafkaConsumerFactory> logger) : IAuditKafkaConsumerFactory
{
    public IAuditKafkaConsumer Create()
    {
        AuditRecordRequestedConsumerOptions consumerOptions = options.Value;
        ConsumerConfig config = CreateConfig(consumerOptions);

        IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => LogKafkaConsumerError(logger, error.Reason, error.IsFatal))
            .Build();

        return new ConfluentAuditKafkaConsumer(consumer);
    }

    internal static ConsumerConfig CreateConfig(AuditRecordRequestedConsumerOptions consumerOptions)
    {
        ArgumentNullException.ThrowIfNull(consumerOptions);

        return KafkaConsumerConfigFactory.Create(consumerOptions);
    }

    internal static AutoOffsetReset ParseAutoOffsetReset(string value)
        => KafkaOffsetResetParser.Parse(value);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Kafka consumer de auditoria reportou erro: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaConsumerError(ILogger logger, string reason, bool isFatal);
}
