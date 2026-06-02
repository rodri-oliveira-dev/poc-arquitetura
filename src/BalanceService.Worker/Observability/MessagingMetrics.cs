using System.Diagnostics.Metrics;

namespace BalanceService.Worker.Observability;

public sealed class MessagingMetrics : IDisposable
{
    public const string MeterName = "BalanceService.Kafka";
    public const string ConsumerMessagesConsumedMetricName = "balance.kafka.consumer.messages.consumed";
    public const string ConsumerProcessingDurationMetricName = "balance.kafka.consumer.processing.duration";
    public const string ConsumerErrorsMetricName = "balance.kafka.consumer.errors";
    public const string ConsumerDuplicatesMetricName = "balance.kafka.consumer.duplicates";
    public const string DlqMessagesPublishedMetricName = "balance.kafka.dlq.messages.published";
    public const string DlqPublishErrorsMetricName = "balance.kafka.dlq.publish.errors";

    private readonly Meter _meter;
    private readonly Counter<long> _consumerMessagesConsumed;
    private readonly Histogram<double> _consumerProcessingDuration;
    private readonly Counter<long> _consumerErrors;
    private readonly Counter<long> _consumerDuplicates;
    private readonly Counter<long> _dlqMessagesPublished;
    private readonly Counter<long> _dlqPublishErrors;

    public MessagingMetrics()
        : this(MeterName)
    {
    }

    public MessagingMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _consumerMessagesConsumed = _meter.CreateCounter<long>(
            ConsumerMessagesConsumedMetricName,
            unit: "1",
            description: "Total de mensagens consumidas pelo Balance por resultado tecnico.");
        _consumerProcessingDuration = _meter.CreateHistogram<double>(
            ConsumerProcessingDurationMetricName,
            unit: "ms",
            description: "Duracao do processamento de mensagens pelo Balance.");
        _consumerErrors = _meter.CreateCounter<long>(
            ConsumerErrorsMetricName,
            unit: "1",
            description: "Total de erros do consumer do Balance por tipo estavel de erro.");
        _consumerDuplicates = _meter.CreateCounter<long>(
            ConsumerDuplicatesMetricName,
            unit: "1",
            description: "Total de mensagens duplicadas ignoradas pela idempotencia do Balance.");
        _dlqMessagesPublished = _meter.CreateCounter<long>(
            DlqMessagesPublishedMetricName,
            unit: "1",
            description: "Total de mensagens publicadas na DLQ pelo Balance.");
        _dlqPublishErrors = _meter.CreateCounter<long>(
            DlqPublishErrorsMetricName,
            unit: "1",
            description: "Total de erros ao publicar mensagens na DLQ pelo Balance.");
    }

    public void RecordConsumerMessageConsumed(string topic, string eventType, string result)
    {
        _consumerMessagesConsumed.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordConsumerProcessingDuration(double elapsedMilliseconds, string topic, string eventType, string result)
    {
        _consumerProcessingDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordConsumerError(string topic, string eventType, string errorType)
    {
        _consumerErrors.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void RecordConsumerDuplicate(string topic, string eventType)
    {
        _consumerDuplicates.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordDlqMessagePublished(string sourceTopic, string eventType, string reason)
    {
        _dlqMessagesPublished.Add(
            1,
            new KeyValuePair<string, object?>("source_topic", sourceTopic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordDlqPublishError(string sourceTopic, string eventType, string errorType)
    {
        _dlqPublishErrors.Add(
            1,
            new KeyValuePair<string, object?>("source_topic", sourceTopic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
