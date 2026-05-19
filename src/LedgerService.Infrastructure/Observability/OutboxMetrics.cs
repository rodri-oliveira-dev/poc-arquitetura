using System.Diagnostics.Metrics;

namespace LedgerService.Infrastructure.Observability;

public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "LedgerService.Outbox";
    public const string PublishAttemptsMetricName = "ledger.outbox.publish.attempts";

    private readonly Meter _meter;
    private readonly Counter<long> _publishAttempts;

    public OutboxMetrics()
        : this(MeterName)
    {
    }

    public OutboxMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _publishAttempts = _meter.CreateCounter<long>(
            PublishAttemptsMetricName,
            unit: "1",
            description: "Total de tentativas tecnicas de publicacao de mensagens Outbox no Kafka.");
    }

    public void RecordPublishAttempt(string eventType, string topic)
    {
        _publishAttempts.Add(
            1,
            new KeyValuePair<string, object?>("service", "LedgerService.Api"),
            new KeyValuePair<string, object?>("operation", "outbox.publish"),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("status", "attempted"));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
