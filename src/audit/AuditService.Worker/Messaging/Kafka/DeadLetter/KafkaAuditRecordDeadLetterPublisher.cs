using System.Text;
using System.Text.Json;

using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal sealed partial class KafkaAuditRecordDeadLetterPublisher(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    AuditWorkerMetrics metrics,
    IAuditKafkaDeadLetterProducerFactory producerFactory,
    ILogger<KafkaAuditRecordDeadLetterPublisher> logger) : IAuditRecordDeadLetterPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AuditRecordRequestedConsumerOptions _options = options.Value;
    private readonly AuditWorkerMetrics _metrics = metrics;
    private readonly ILogger<KafkaAuditRecordDeadLetterPublisher> _logger = logger;
    private readonly IAuditKafkaDeadLetterProducer _producer = producerFactory.Create();

    public async Task PublishAsync(AuditRecordDeadLetterMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_options.DeadLetterTopic))
            throw new InvalidOperationException("AuditRecordRequested Consumer DeadLetterTopic nao configurado.");

        var headers = new Headers
        {
            { "dlq_reason", Encoding.UTF8.GetBytes(message.FailureReason) },
            { "dlq_category", Encoding.UTF8.GetBytes(message.FailureCategory) },
            { "original_topic", Encoding.UTF8.GetBytes(message.OriginalTopic) },
            { "original_partition", Encoding.UTF8.GetBytes(message.OriginalPartition.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            { "original_offset", Encoding.UTF8.GetBytes(message.OriginalOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)) }
        };

        AddHeaderIfPresent(headers, "event_id", message.EventId?.ToString());
        AddHeaderIfPresent(headers, "correlation_id", message.CorrelationId?.ToString());

        try
        {
            DeliveryResult<string, string> result = await _producer.ProduceAsync(
                _options.DeadLetterTopic,
                new Message<string, string>
                {
                    Key = $"{message.OriginalTopic}:{message.OriginalPartition}:{message.OriginalOffset}",
                    Value = JsonSerializer.Serialize(message, JsonOptions),
                    Headers = headers,
                    Timestamp = new Timestamp(message.OccurredAt.UtcDateTime)
                },
                cancellationToken);

            _metrics.RecordDlqPublished(message.OriginalTopic, message.FailureCategory);

            LogAuditRecordRequestedPublishedToDlq(
                _logger,
                _options.DeadLetterTopic,
                result.Partition.Value,
                result.Offset.Value,
                message.OriginalTopic,
                message.OriginalPartition,
                message.OriginalOffset,
                message.EventId,
                message.CorrelationId,
                message.FailureCategory);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or TimeoutException or InvalidOperationException)
        {
            _metrics.RecordDlqPublishError(message.OriginalTopic, ex.GetType().Name);
            throw;
        }
    }

    private static void AddHeaderIfPresent(Headers headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            headers.Add(name, Encoding.UTF8.GetBytes(value));
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch (KafkaException)
        {
            // ignore shutdown errors
        }

        _producer.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "AuditRecordRequested.v1 publicado na DLQ {DeadLetterTopic}. partition={Partition} offset={Offset} originalTopic={OriginalTopic} originalPartition={OriginalPartition} originalOffset={OriginalOffset} eventId={EventId} correlationId={CorrelationId} failureCategory={FailureCategory}")]
    private static partial void LogAuditRecordRequestedPublishedToDlq(
        ILogger logger,
        string deadLetterTopic,
        int partition,
        long offset,
        string originalTopic,
        int originalPartition,
        long originalOffset,
        Guid? eventId,
        Guid? correlationId,
        string failureCategory);
}
