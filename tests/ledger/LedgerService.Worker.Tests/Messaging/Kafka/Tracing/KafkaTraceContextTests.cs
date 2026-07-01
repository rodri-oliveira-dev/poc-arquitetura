using System.Diagnostics;

using Confluent.Kafka;

using LedgerService.Worker.Messaging.Kafka.Tracing;

namespace LedgerService.Worker.Tests.Messaging.Kafka.Tracing;

public sealed class KafkaTraceContextTests
{
    [Fact]
    public void StartProducerActivity_DeveRestaurarParentW3cEReidratarBaggage()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "LedgerService.Tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("LedgerService.Tests");

        using var activity = KafkaTraceContext.StartProducerActivity(
            source,
            "outbox.publish",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "vendor=value",
            "tenant=poc");

        Assert.NotNull(activity);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", activity!.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", activity.ParentSpanId.ToString());
        Assert.Equal("poc", activity.Baggage.Single(x => x.Key == "tenant").Value);
    }

    [Fact]
    public void AddPropagationHeaders_DeveIgnorarValoresAusentes()
    {
        var headers = new Headers();

        KafkaTraceContext.AddPropagationHeaders(
            headers,
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            null,
            "");

        Assert.Contains(headers, x => x.Key == KafkaHeaderNames.TraceParent);
        Assert.DoesNotContain(headers, x => x.Key == KafkaHeaderNames.TraceState);
        Assert.DoesNotContain(headers, x => x.Key == KafkaHeaderNames.Baggage);
    }
}
