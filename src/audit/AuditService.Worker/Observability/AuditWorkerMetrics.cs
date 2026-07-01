using System.Diagnostics.Metrics;

namespace AuditService.Worker.Observability;

public sealed class AuditWorkerMetrics : IDisposable
{
    public const string MeterName = "AuditService.Worker";
    public const string ConsumerMessagesMetricName = "audit.worker.consumer.messages";
    public const string ConsumerProcessingDurationMetricName = "audit.worker.consumer.processing.duration";
    public const string ConsumerRetriesMetricName = "audit.worker.consumer.retries";
    public const string DlqMessagesMetricName = "audit.worker.dlq.messages";
    public const string DlqPublishErrorsMetricName = "audit.worker.dlq.publish.errors";

    private readonly Meter _meter;
    private readonly Counter<long> _consumerMessages;
    private readonly Histogram<double> _consumerProcessingDuration;
    private readonly Counter<long> _consumerRetries;
    private readonly Counter<long> _dlqMessages;
    private readonly Counter<long> _dlqPublishErrors;

    public AuditWorkerMetrics()
        : this(MeterName)
    {
    }

    public AuditWorkerMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _consumerMessages = _meter.CreateCounter<long>(
            ConsumerMessagesMetricName,
            unit: "1",
            description: "Total de mensagens AuditRecordRequested consumidas por resultado tecnico.");
        _consumerProcessingDuration = _meter.CreateHistogram<double>(
            ConsumerProcessingDurationMetricName,
            unit: "ms",
            description: "Duracao do processamento de AuditRecordRequested.");
        _consumerRetries = _meter.CreateCounter<long>(
            ConsumerRetriesMetricName,
            unit: "1",
            description: "Total de retries locais do consumer AuditRecordRequested.");
        _dlqMessages = _meter.CreateCounter<long>(
            DlqMessagesMetricName,
            unit: "1",
            description: "Total de mensagens AuditRecordRequested publicadas na DLQ.");
        _dlqPublishErrors = _meter.CreateCounter<long>(
            DlqPublishErrorsMetricName,
            unit: "1",
            description: "Total de erros ao publicar AuditRecordRequested na DLQ.");
    }

    public void RecordConsumerMessage(string topic, string result)
        => _consumerMessages.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("result", result));

    public void RecordConsumerProcessingDuration(double elapsedMilliseconds, string topic, string result)
        => _consumerProcessingDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("result", result));

    public void RecordConsumerRetry(string topic, string errorType)
        => _consumerRetries.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("error_type", errorType));

    public void RecordDlqPublished(string sourceTopic, string failureCategory)
        => _dlqMessages.Add(
            1,
            new KeyValuePair<string, object?>("source_topic", sourceTopic),
            new KeyValuePair<string, object?>("failure_category", failureCategory));

    public void RecordDlqPublishError(string sourceTopic, string errorType)
        => _dlqPublishErrors.Add(
            1,
            new KeyValuePair<string, object?>("source_topic", sourceTopic),
            new KeyValuePair<string, object?>("error_type", errorType));

    public void Dispose()
        => _meter.Dispose();
}
