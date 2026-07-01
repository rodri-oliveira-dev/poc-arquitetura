using AuditService.Worker.Messaging.Kafka.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class ConfluentAuditKafkaConsumerFactory(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    ILogger<ConfluentAuditKafkaConsumerFactory> logger) : IAuditKafkaConsumerFactory
{
    public IAuditKafkaConsumer Create()
    {
        AuditRecordRequestedConsumerOptions consumerOptions = options.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = consumerOptions.BootstrapServers,
            GroupId = consumerOptions.GroupId,
            ClientId = consumerOptions.ClientId,
            EnableAutoCommit = consumerOptions.EnableAutoCommit,
            EnableAutoOffsetStore = consumerOptions.EnableAutoOffsetStore,
            AllowAutoCreateTopics = consumerOptions.AllowAutoCreateTopics,
            AutoOffsetReset = ParseAutoOffsetReset(consumerOptions.AutoOffsetReset)
        };
        config.ApplySecurity(consumerOptions);

        IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => LogKafkaConsumerError(logger, error.Reason, error.IsFatal))
            .Build();

        return new ConfluentAuditKafkaConsumer(consumer);
    }

    internal static AutoOffsetReset ParseAutoOffsetReset(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Kafka consumer de auditoria reportou erro: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaConsumerError(ILogger logger, string reason, bool isFatal);
}
