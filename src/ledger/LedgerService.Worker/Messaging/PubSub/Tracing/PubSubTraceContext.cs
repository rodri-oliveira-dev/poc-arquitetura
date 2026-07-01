using System.Diagnostics;

namespace LedgerService.Worker.Messaging.PubSub.Tracing;

public static class PubSubTraceContext
{
    public static void AddPropagationAttributes(
        IDictionary<string, string> attributes,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        AddIfPresent(attributes, PubSubAttributeNames.TraceParent, traceParent);
        AddIfPresent(attributes, PubSubAttributeNames.TraceState, traceState);
        AddIfPresent(attributes, PubSubAttributeNames.Baggage, baggage);
    }

    public static string? FormatCurrentBaggage()
    {
        Activity? activity = Activity.Current;
        if (activity is null)
            return null;

        string baggage = string.Join(",", activity.Baggage.Select(x => $"{x.Key}={x.Value}"));
        return string.IsNullOrWhiteSpace(baggage) ? null : baggage;
    }

    private static void AddIfPresent(
        IDictionary<string, string> attributes,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            attributes.Add(name, value);
    }
}
