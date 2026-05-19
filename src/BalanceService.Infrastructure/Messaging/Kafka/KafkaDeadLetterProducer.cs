using System.Globalization;
using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using BalanceService.Infrastructure.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class KafkaDeadLetterProducer : IKafkaDeadLetterProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaConsumerOptions _options;
    private readonly KafkaMessagingMetrics _metrics;
    private readonly ILogger<KafkaDeadLetterProducer> _logger;
    private readonly IProducer<string, string> _producer;

    public KafkaDeadLetterProducer(
        IOptions<KafkaConsumerOptions> options,
        KafkaMessagingMetrics metrics,
        ILogger<KafkaDeadLetterProducer> logger)
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

    public async Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DeadLetterTopic))
            throw new InvalidOperationException("Kafka DeadLetterTopic não configurado.");

        var headers = new Headers
        {
            { "dlq_reason", Encoding.UTF8.GetBytes(message.Reason) },
            { "original_topic", Encoding.UTF8.GetBytes(message.OriginalTopic) },
            { "original_partition", Encoding.UTF8.GetBytes(message.OriginalPartition.ToString(CultureInfo.InvariantCulture)) },
            { "original_offset", Encoding.UTF8.GetBytes(message.OriginalOffset.ToString(CultureInfo.InvariantCulture)) }
        };

        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.EventType);
        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.EventId);
        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.CorrelationId);
        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.TraceParent);
        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.TraceState);
        KafkaTraceContext.CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.Baggage);

        try
        {
            var result = await _producer.ProduceAsync(
                _options.DeadLetterTopic,
                new Message<string, string>
                {
                    Key = $"{message.OriginalTopic}:{message.OriginalPartition}:{message.OriginalOffset}",
                    Value = JsonSerializer.Serialize(message, JsonOptions),
                    Headers = headers,
                    Timestamp = new Timestamp(message.Timestamp.UtcDateTime)
                },
                cancellationToken);

            _metrics.RecordDlqMessagePublished(
                message.OriginalTopic,
                ResolveEventType(message.OriginalHeaders),
                ClassifyReason(message.Reason));

            _logger.LogWarning(
                "Kafka message published to DLQ {DeadLetterTopic} [partition={Partition}, offset={Offset}] from {OriginalTopic}/{OriginalPartition}/{OriginalOffset}",
                _options.DeadLetterTopic,
                result.Partition.Value,
                result.Offset.Value,
                message.OriginalTopic,
                message.OriginalPartition,
                message.OriginalOffset);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or TimeoutException or InvalidOperationException)
        {
            _metrics.RecordDlqPublishError(
                message.OriginalTopic,
                ResolveEventType(message.OriginalHeaders),
                ex.GetType().Name);

            throw;
        }
    }

    private static string ResolveEventType(IReadOnlyDictionary<string, string> headers)
        => headers.TryGetValue(KafkaHeaderNames.EventType, out var eventType) && !string.IsNullOrWhiteSpace(eventType)
            ? eventType
            : "unknown";

    private static string ClassifyReason(string reason)
    {
        if (string.Equals(reason, "Deserialization failed.", StringComparison.Ordinal))
            return "deserialization_failed";

        if (string.Equals(reason, "Non-recoverable processing failure.", StringComparison.Ordinal))
            return "non_recoverable_processing_failure";

        if (reason.StartsWith("Missing required Kafka header", StringComparison.Ordinal) ||
            reason.StartsWith("Unsupported Kafka event_type", StringComparison.Ordinal) ||
            reason.StartsWith("Message payload", StringComparison.Ordinal))
        {
            return "validation_failed";
        }

        return "unknown";
    }

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
