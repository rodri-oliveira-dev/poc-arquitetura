using System.Diagnostics.Metrics;

using FluentAssertions;

using LedgerService.Infrastructure.Observability;

namespace LedgerService.UnitTests.Infrastructure.Observability;

public sealed class OutboxMetricsTests
{
    private static readonly string[] ProhibitedTags =
    [
        "correlation_id",
        "trace_id",
        "span_id",
        "event_id",
        "outbox_message_id",
        "merchant_id"
    ];

    [Fact]
    public void RecordPublishAttempt_should_emit_low_cardinality_tags_without_opentelemetry_provider()
    {
        var meterName = $"{OutboxMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new OutboxMetrics(meterName);

        long? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == OutboxMetrics.PublishAttemptsMetricName)
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

        metrics.RecordPublishAttempt("LedgerEntryCreated.v1", "success");

        measurement.Should().Be(1);
        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("event_type", "LedgerEntryCreated.v1");
        observedTags.Should().Contain("result", "success");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }
}
