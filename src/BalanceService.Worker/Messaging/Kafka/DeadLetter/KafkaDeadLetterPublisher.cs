using System.Text;
using System.Text.Json;

using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Tracing;
using BalanceService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace BalanceService.Worker.Messaging.Kafka.DeadLetter;

public sealed class KafkaDeadLetterPublisher : IDeadLetterPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaConsumerOptions _options;
    private readonly MessagingMetrics _metrics;
    private readonly ILogger<KafkaDeadLetterPublisher> _logger;
    private readonly IProducer<string, string> _producer;

    public KafkaDeadLetterPublisher(
        IOptions<KafkaConsumerOptions> options,
        MessagingMetrics metrics,
        ILogger<KafkaDeadLetterPublisher> logger)
    {
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-dlq",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = _options.DeadLetterMessageTimeoutMs
        };
        config.ApplySecurity(_options);

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning("Kafka DLQ producer error: {Reason} (IsFatal={IsFatal})", e.Reason, e.IsFatal);
            })
            .Build();
    }

    public async Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DeadLetterTopic))
            throw new InvalidOperationException("Kafka DeadLetterTopic nÃ£o configurado.");

        var originalTopic = ResolveTransportMetadata(message, "topic", message.Source);
        var originalPartition = ResolveTransportMetadata(message, "partition", "0");
        var originalOffset = ResolveTransportMetadata(message, "offset", "0");

        var headers = new Headers
        {
            { "dlq_reason", Encoding.UTF8.GetBytes(message.Reason) },
            { "original_topic", Encoding.UTF8.GetBytes(originalTopic) },
            { "original_partition", Encoding.UTF8.GetBytes(originalPartition) },
            { "original_offset", Encoding.UTF8.GetBytes(originalOffset) }
        };

        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.EventType);
        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.EventId);
        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.CorrelationId);
        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.TraceParent);
        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.TraceState);
        KafkaTraceContext.CopyHeaderIfPresent(message.Attributes, headers, MessageAttributeNames.Baggage);

        try
        {
            var result = await _producer.ProduceAsync(
                _options.DeadLetterTopic,
                new Message<string, string>
                {
                    Key = $"{originalTopic}:{originalPartition}:{originalOffset}",
                    Value = JsonSerializer.Serialize(message, JsonOptions),
                    Headers = headers,
                    Timestamp = new Timestamp(message.Timestamp.UtcDateTime)
                },
                cancellationToken);

            _metrics.RecordDlqMessagePublished(
                originalTopic,
                ResolveEventType(message.Attributes),
                ClassifyReason(message.Reason));

            _logger.LogWarning(
                "Kafka message published to DLQ {DeadLetterTopic} [partition={Partition}, offset={Offset}] from {OriginalTopic}/{OriginalPartition}/{OriginalOffset}",
                _options.DeadLetterTopic,
                result.Partition.Value,
                result.Offset.Value,
                originalTopic,
                originalPartition,
                originalOffset);
        }
        catch (ProduceException<string, string> ex)
        {
            RecordDlqPublishError(message, originalTopic, ex);
            throw;
        }
        catch (KafkaException ex)
        {
            RecordDlqPublishError(message, originalTopic, ex);
            throw;
        }
        catch (TimeoutException ex)
        {
            RecordDlqPublishError(message, originalTopic, ex);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            RecordDlqPublishError(message, originalTopic, ex);
            throw;
        }
    }

    internal static string ResolveEventType(IReadOnlyDictionary<string, string> attributes)
        => attributes.TryGetValue(MessageAttributeNames.EventType, out var eventType) && !string.IsNullOrWhiteSpace(eventType)
            ? eventType
            : "unknown";

    internal static string ClassifyReason(string reason)
    {
        if (string.Equals(reason, "Deserialization failed.", StringComparison.Ordinal))
            return "deserialization_failed";

        if (string.Equals(reason, "Non-recoverable processing failure.", StringComparison.Ordinal))
            return "non_recoverable_processing_failure";

        if (reason.StartsWith("Missing required message attribute", StringComparison.Ordinal) ||
            reason.StartsWith("Unsupported message event_type", StringComparison.Ordinal) ||
            reason.StartsWith("Message payload", StringComparison.Ordinal))
        {
            return "validation_failed";
        }

        return "unknown";
    }

    private static string ResolveTransportMetadata(DeadLetterMessage message, string name, string fallback)
        => message.TransportMetadata.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private void RecordDlqPublishError(DeadLetterMessage message, string originalTopic, Exception exception)
        => _metrics.RecordDlqPublishError(
            originalTopic,
            ResolveEventType(message.Attributes),
            exception.GetType().Name);

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch (KafkaException)
        {
            // ignore
        }

        _producer.Dispose();
    }
}
