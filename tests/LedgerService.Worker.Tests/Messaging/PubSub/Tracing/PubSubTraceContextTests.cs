using System.Diagnostics;

using LedgerService.Worker.Messaging.PubSub.Tracing;

namespace LedgerService.Worker.Tests.Messaging.PubSub.Tracing;

public sealed class PubSubTraceContextTests
{
    [Fact]
    public void AddPropagationAttributes_should_add_present_values()
    {
        Dictionary<string, string> attributes = new();

        PubSubTraceContext.AddPropagationAttributes(
            attributes,
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "vendor=value",
            "tenant=poc");

        Assert.Equal(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            attributes[PubSubAttributeNames.TraceParent]);
        Assert.Equal("vendor=value", attributes[PubSubAttributeNames.TraceState]);
        Assert.Equal("tenant=poc", attributes[PubSubAttributeNames.Baggage]);
    }

    [Fact]
    public void AddPropagationAttributes_should_ignore_missing_values()
    {
        Dictionary<string, string> attributes = new();

        PubSubTraceContext.AddPropagationAttributes(
            attributes,
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            null,
            "");

        Assert.Contains(PubSubAttributeNames.TraceParent, attributes);
        Assert.DoesNotContain(PubSubAttributeNames.TraceState, attributes);
        Assert.DoesNotContain(PubSubAttributeNames.Baggage, attributes);
    }

    [Fact]
    public void FormatCurrentBaggage_should_serialize_activity_baggage()
    {
        using Activity activity = new("PubSubTraceContextTests");
        activity.AddBaggage("tenant", "poc");
        activity.AddBaggage("merchant", "merchant-1");
        activity.Start();

        string? baggage = PubSubTraceContext.FormatCurrentBaggage();

        Assert.NotNull(baggage);
        Assert.Contains("tenant=poc", baggage, StringComparison.Ordinal);
        Assert.Contains("merchant=merchant-1", baggage, StringComparison.Ordinal);
    }
}
