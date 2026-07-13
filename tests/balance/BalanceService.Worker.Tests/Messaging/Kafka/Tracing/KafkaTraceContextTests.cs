using System.Diagnostics;
using System.Text;

using BalanceService.Worker.Messaging.Kafka.Tracing;

using Confluent.Kafka;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Tracing;

public sealed class KafkaTraceContextTests
{
    [Fact]
    public void ReadHeaders_should_return_empty_dictionary_when_headers_are_null()
    {
        IReadOnlyDictionary<string, string> headers = KafkaTraceContext.ReadHeaders(null);

        Assert.Empty(headers);
    }

    [Fact]
    public void ReadHeaders_should_decode_kafka_headers()
    {
        var kafkaHeaders = new Headers
        {
            { "event_type", Encoding.UTF8.GetBytes("LedgerEntryCreated.v1") },
            { "correlation_id", Encoding.UTF8.GetBytes("corr-1") }
        };

        IReadOnlyDictionary<string, string> headers = KafkaTraceContext.ReadHeaders(kafkaHeaders);

        Assert.Equal("LedgerEntryCreated.v1", headers["event_type"]);
        Assert.Equal("corr-1", headers["CORRELATION_ID"]);
    }

    [Fact]
    public void CopyHeaderIfPresent_should_copy_only_present_non_empty_header()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["event_id"] = "evt-1",
            ["empty"] = " "
        };
        var target = new Headers();

        KafkaTraceContext.CopyHeaderIfPresent(source, target, "event_id");
        KafkaTraceContext.CopyHeaderIfPresent(source, target, "empty");
        KafkaTraceContext.CopyHeaderIfPresent(source, target, "missing");

        IHeader header = Assert.Single(target);
        Assert.Equal("event_id", header.Key);
        Assert.Equal("evt-1", Encoding.UTF8.GetString(header.GetValueBytes()));
    }

    [Fact]
    public void StartConsumerActivity_should_use_parent_context_and_apply_valid_baggage_items()
    {
        using var activitySource = new ActivitySource("balance-worker-tests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        using Activity? activity = KafkaTraceContext.StartConsumerActivity(
            activitySource,
            "consume ledger event",
            "00-" + parent.TraceId + "-" + parent.SpanId + "-01",
            traceState: "vendor=value",
            baggage: "merchant=merchant-1, invalid, empty=, region=br;property=value");

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity!.Kind);
        Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.Equal("merchant-1", activity.GetBaggageItem("merchant"));
        Assert.Equal("br", activity.GetBaggageItem("region"));
        Assert.Null(activity.GetBaggageItem("invalid"));
        Assert.Null(activity.GetBaggageItem("empty"));
    }
}
