using System.Diagnostics.Metrics;


using LedgerService.Application.Common.Observability;

namespace LedgerService.UnitTests.Application.Common.Observability;

public sealed class LedgerDomainMetricsTests
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
    public void RecordEntryCreated_should_emit_only_low_cardinality_tags()
    {
        var meterName = $"{LedgerDomainMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new LedgerDomainMetrics(meterName);

        long? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == LedgerDomainMetrics.EntriesCreatedMetricName)
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

        metrics.RecordEntryCreated("CREDIT", "BRL", "success");
        Assert.Equal(1, measurement);
        Assert.NotNull(observedTags);
        Assert.Equal("CREDIT", observedTags["entry_type"]);
        Assert.Equal("BRL", observedTags["currency"]);
        Assert.Equal("success", observedTags["result"]);
        Assert.Empty(ProhibitedTags.Intersect(observedTags!.Keys));
    }

    [Fact]
    public void RecordIdempotencyHit_should_tag_operation_without_idempotency_key()
    {
        var meterName = $"{LedgerDomainMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new LedgerDomainMetrics(meterName);

        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == LedgerDomainMetrics.IdempotencyHitsMetricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            observedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
        });

        listener.Start();

        metrics.RecordIdempotencyHit("create_entry");
        Assert.NotNull(observedTags);
        Assert.Equal("create_entry", observedTags["operation"]);
        Assert.Empty(ProhibitedTags.Intersect(observedTags!.Keys));
    }
}
