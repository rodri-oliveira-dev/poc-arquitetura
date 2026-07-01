using AuditService.Worker.Messaging.Kafka.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal sealed partial class ConfluentAuditKafkaDeadLetterProducerFactory(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    ILogger<ConfluentAuditKafkaDeadLetterProducerFactory> logger) : IAuditKafkaDeadLetterProducerFactory
{
    public IAuditKafkaDeadLetterProducer Create()
    {
        AuditRecordRequestedConsumerOptions consumerOptions = options.Value;
        ProducerConfig config = CreateConfig(consumerOptions);

        IProducer<string, string> producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => LogKafkaDlqProducerError(logger, error.Reason, error.IsFatal))
            .Build();

        return new ConfluentAuditKafkaDeadLetterProducer(producer);
    }

    internal static ProducerConfig CreateConfig(AuditRecordRequestedConsumerOptions consumerOptions)
    {
        ArgumentNullException.ThrowIfNull(consumerOptions);

        var config = new ProducerConfig
        {
            BootstrapServers = consumerOptions.BootstrapServers,
            ClientId = $"{consumerOptions.ClientId}-dlq",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = consumerOptions.DeadLetterMessageTimeoutMs
        };
        config.ApplySecurity(consumerOptions);

        return config;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Kafka DLQ producer de auditoria reportou erro: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaDlqProducerError(ILogger logger, string reason, bool isFatal);
}
