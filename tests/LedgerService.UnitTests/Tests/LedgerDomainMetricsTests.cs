using System.Diagnostics.Metrics;

using FluentAssertions;

using LedgerService.Application.Common.Observability;

namespace LedgerService.UnitTests.Tests;

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

        measurement.Should().Be(1);
        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("entry_type", "CREDIT");
        observedTags.Should().Contain("currency", "BRL");
        observedTags.Should().Contain("result", "success");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
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

        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("operation", "create_entry");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }
}
