using System.Diagnostics.Metrics;

using BalanceService.Worker.Observability;


namespace BalanceService.Worker.Tests.Observability;

public sealed class MessagingMetricsTests
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
    public void Metric_names_should_preserve_existing_dashboard_compatibility()
    {
        Assert.Equal("BalanceService.Kafka", MessagingMetrics.MeterName);
        Assert.Equal("balance.kafka.consumer.messages.consumed", MessagingMetrics.ConsumerMessagesConsumedMetricName);
        Assert.Equal("balance.kafka.consumer.processing.duration", MessagingMetrics.ConsumerProcessingDurationMetricName);
        Assert.Equal("balance.kafka.consumer.errors", MessagingMetrics.ConsumerErrorsMetricName);
        Assert.Equal("balance.kafka.consumer.duplicates", MessagingMetrics.ConsumerDuplicatesMetricName);
        Assert.Equal("balance.kafka.dlq.messages.published", MessagingMetrics.DlqMessagesPublishedMetricName);
        Assert.Equal("balance.kafka.dlq.publish.errors", MessagingMetrics.DlqPublishErrorsMetricName);
    }

    [Fact]
    public void RecordConsumerMessageConsumed_should_emit_only_low_cardinality_tags()
    {
        var meterName = $"{MessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new MessagingMetrics(meterName);

        long? measurement = null;
        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == MessagingMetrics.ConsumerMessagesConsumedMetricName)
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
        Assert.Equal(1, measurement);
        Assert.NotNull(observedTags);
        Assert.Equal("ledger.ledgerentry.created", observedTags["topic"]);
        Assert.Equal("LedgerEntryCreated.v1", observedTags["event_type"]);
        Assert.Equal("success", observedTags["result"]);
        Assert.Empty(ProhibitedTags.Intersect(observedTags!.Keys));
    }

    [Fact]
    public void RecordDlqMessagePublished_should_use_stable_reason_tag()
    {
        var meterName = $"{MessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new MessagingMetrics(meterName);

        Dictionary<string, object?>? observedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                instrument.Name == MessagingMetrics.DlqMessagesPublishedMetricName)
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
        Assert.NotNull(observedTags);
        Assert.Equal("ledger.ledgerentry.created", observedTags["source_topic"]);
        Assert.Equal("LedgerEntryCreated.v1", observedTags["event_type"]);
        Assert.Equal("validation_failed", observedTags["reason"]);
        Assert.Empty(ProhibitedTags.Intersect(observedTags!.Keys));
    }

    [Fact]
    public void MessagingMetrics_should_record_remaining_low_cardinality_metrics()
    {
        var meterName = $"{MessagingMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        var expectedMetricNames = new[]
        {
            MessagingMetrics.ConsumerProcessingDurationMetricName,
            MessagingMetrics.ConsumerErrorsMetricName,
            MessagingMetrics.ConsumerDuplicatesMetricName,
            MessagingMetrics.DlqPublishErrorsMetricName
        };
        using var listener = new MeterListener();
        using var metrics = new MessagingMetrics(meterName);

        var observedMetrics = new Dictionary<string, ObservedMetric>(StringComparer.Ordinal);
        var enabledMetricNames = expectedMetricNames.ToHashSet(StringComparer.Ordinal);

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName &&
                enabledMetricNames.Contains(instrument.Name))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            observedMetrics[instrument.Name] = new ObservedMetric(
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        });

        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            observedMetrics[instrument.Name] = new ObservedMetric(
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        });

        listener.Start();

        metrics.RecordConsumerProcessingDuration(12.5, "ledger.ledgerentry.created", "LedgerEntryCreated.v1", "success");
        metrics.RecordConsumerError("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "TimeoutException");
        metrics.RecordConsumerDuplicate("ledger.ledgerentry.created", "LedgerEntryCreated.v1");
        metrics.RecordDlqPublishError("ledger.ledgerentry.created", "LedgerEntryCreated.v1", "KafkaException");

        Assert.Equal(expectedMetricNames.Order(), observedMetrics.Keys.Order());
        AssertObservedMetric(
            observedMetrics,
            MessagingMetrics.ConsumerProcessingDurationMetricName,
            12.5,
            new Dictionary<string, object?>
            {
                ["topic"] = "ledger.ledgerentry.created",
                ["event_type"] = "LedgerEntryCreated.v1",
                ["result"] = "success"
            });
        AssertObservedMetric(
            observedMetrics,
            MessagingMetrics.ConsumerErrorsMetricName,
            1L,
            new Dictionary<string, object?>
            {
                ["topic"] = "ledger.ledgerentry.created",
                ["event_type"] = "LedgerEntryCreated.v1",
                ["error_type"] = "TimeoutException"
            });
        AssertObservedMetric(
            observedMetrics,
            MessagingMetrics.ConsumerDuplicatesMetricName,
            1L,
            new Dictionary<string, object?>
            {
                ["topic"] = "ledger.ledgerentry.created",
                ["event_type"] = "LedgerEntryCreated.v1"
            });
        AssertObservedMetric(
            observedMetrics,
            MessagingMetrics.DlqPublishErrorsMetricName,
            1L,
            new Dictionary<string, object?>
            {
                ["source_topic"] = "ledger.ledgerentry.created",
                ["event_type"] = "LedgerEntryCreated.v1",
                ["error_type"] = "KafkaException"
            });
    }

    private static void AssertObservedMetric(
        IReadOnlyDictionary<string, ObservedMetric> observedMetrics,
        string metricName,
        object expectedValue,
        IReadOnlyDictionary<string, object?> expectedTags)
    {
        Assert.True(observedMetrics.TryGetValue(metricName, out var metric), $"Metric '{metricName}' was not emitted.");
        Assert.Equal(expectedValue, metric.Value);

        foreach (var expectedTag in expectedTags)
        {
            Assert.True(
                metric.Tags.TryGetValue(expectedTag.Key, out var actualValue),
                $"Metric '{metricName}' did not emit expected tag '{expectedTag.Key}'.");
            Assert.Equal(expectedTag.Value, actualValue);
        }

        Assert.Empty(ProhibitedTags.Intersect(metric.Tags.Keys));
    }

    private sealed record ObservedMetric(object Value, IReadOnlyDictionary<string, object?> Tags);
}
