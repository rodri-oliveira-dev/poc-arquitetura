using System.Diagnostics.Metrics;

using BalanceService.Application.Common.Observability;

using FluentAssertions;

namespace BalanceService.UnitTests.Application.Common.Observability;

public sealed class BalanceDomainMetricsTests
{
    private static readonly string[] ProhibitedTags =
    [
        "merchant_id",
        "ledger_entry_id",
        "event_id",
        "correlation_id",
        "trace_id",
        "span_id",
        "document",
        "external_reference",
        "idempotency_key",
        "amount",
        "description",
        "exception_message"
    ];

    [Fact]
    public void RecordEventApplied_should_emit_only_low_cardinality_tags()
    {
        var meterName = $"{BalanceDomainMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new BalanceDomainMetrics(meterName);

        long? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == BalanceDomainMetrics.EventsAppliedMetricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            measurement = value;
            observedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
        });

        listener.Start();

        metrics.RecordEventApplied("LedgerEntryCreated.v1", "success");

        measurement.Should().Be(1);
        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("event_type", "LedgerEntryCreated.v1");
        observedTags.Should().Contain("result", "success");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }

    [Fact]
    public void RecordApplyDuration_should_use_event_type_and_result_only()
    {
        var meterName = $"{BalanceDomainMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new BalanceDomainMetrics(meterName);

        double? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == BalanceDomainMetrics.ApplyDurationMetricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            measurement = value;
            observedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
        });

        listener.Start();

        metrics.RecordApplyDuration(12.5, "LedgerEntryCreated.v1", "success");

        measurement.Should().Be(12.5);
        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("event_type", "LedgerEntryCreated.v1");
        observedTags.Should().Contain("result", "success");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }
}
