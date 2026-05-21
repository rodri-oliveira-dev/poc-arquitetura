using System.Diagnostics.Metrics;

using BalanceService.Worker.Observability;

using FluentAssertions;

namespace BalanceService.Worker.Tests.Observability;

public sealed class KafkaMessagingMetricsTests
{
    private static readonly string[] ProhibitedTags =
    [
        "correlation_id",
        "trace_id",
        "span_id",
        "event_id",
        "outbox_message_id",
        "merchant_id",
        "partition",
        "offset",
        "payload"
    ];

    [Fact]
    public void RecordConsumerMessageConsumed_should_emit_only_low_cardinality_tags()
    {
        var meterName = $"{KafkaMessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new KafkaMessagingMetrics(meterName);

        long? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == KafkaMessagingMetrics.ConsumerMessagesConsumedMetricName)
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

        metrics.RecordConsumerMessageConsumed("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "success");

        measurement.Should().Be(1);
        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("topic", "ledger.ledgerentry.created");
        observedTags.Should().Contain("event_type", "LedgerEntryCreated.v1");
        observedTags.Should().Contain("result", "success");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }

    [Fact]
    public void RecordDlqMessagePublished_should_use_stable_reason_tag()
    {
        var meterName = $"{KafkaMessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new KafkaMessagingMetrics(meterName);

        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == KafkaMessagingMetrics.DlqMessagesPublishedMetricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            observedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
        });

        listener.Start();

        metrics.RecordDlqMessagePublished("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "validation_failed");

        observedTags.Should().NotBeNull();
        observedTags.Should().Contain("source_topic", "ledger.ledgerentry.created");
        observedTags.Should().Contain("event_type", "LedgerEntryCreated.v1");
        observedTags.Should().Contain("reason", "validation_failed");
        observedTags!.Keys.Should().NotContain(ProhibitedTags);
    }

    [Fact]
    public void KafkaMessagingMetrics_should_record_remaining_low_cardinality_metrics()
    {
        using var metrics = new KafkaMessagingMetrics($"{KafkaMessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}");

        metrics.RecordConsumerProcessingDuration(12.5, "ledger.ledgerentry.created", "LedgerEntryCreated.v1", "success");
        metrics.RecordConsumerError("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "TimeoutException");
        metrics.RecordConsumerDuplicate("ledger.ledgerentry.created", "LedgerEntryCreated.v1");
        metrics.RecordDlqPublishError("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "KafkaException");
    }
}
