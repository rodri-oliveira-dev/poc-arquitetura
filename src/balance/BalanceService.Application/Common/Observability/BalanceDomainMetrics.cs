using System.Diagnostics.Metrics;

namespace BalanceService.Application.Common.Observability;

public sealed class BalanceDomainMetrics : IDisposable
{
    public const string MeterName = "BalanceService.Domain";
    public const string EventsAppliedMetricName = "balance.events.applied";
    public const string EventsDuplicatesMetricName = "balance.events.duplicates";
    public const string ProjectionsUpdatedMetricName = "balance.projections.updated";
    public const string ApplyDurationMetricName = "balance.apply.duration";

    private readonly Meter _meter;
    private readonly Counter<long> _eventsApplied;
    private readonly Counter<long> _eventsDuplicates;
    private readonly Counter<long> _projectionsUpdated;
    private readonly Histogram<double> _applyDuration;

    public BalanceDomainMetrics()
        : this(MeterName)
    {
    }

    public BalanceDomainMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _eventsApplied = _meter.CreateCounter<long>(
            EventsAppliedMetricName,
            unit: "1",
            description: "Total de eventos financeiros tratados pela projecao do Balance por resultado.");
        _eventsDuplicates = _meter.CreateCounter<long>(
            EventsDuplicatesMetricName,
            unit: "1",
            description: "Total de eventos financeiros duplicados ignorados pela idempotencia do Balance.");
        _projectionsUpdated = _meter.CreateCounter<long>(
            ProjectionsUpdatedMetricName,
            unit: "1",
            description: "Total de projecoes de saldo atualizadas pelo Balance por moeda.");
        _applyDuration = _meter.CreateHistogram<double>(
            ApplyDurationMetricName,
            unit: "ms",
            description: "Duracao da aplicacao de evento financeiro na projecao de saldo do Balance.");
    }

    public void RecordEventApplied(string eventType, string result)
    {
        _eventsApplied.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordEventDuplicate(string eventType)
    {
        _eventsDuplicates.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordProjectionUpdated(string currency)
    {
        _projectionsUpdated.Add(
            1,
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordApplyDuration(double elapsedMilliseconds, string eventType, string result)
    {
        _applyDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
