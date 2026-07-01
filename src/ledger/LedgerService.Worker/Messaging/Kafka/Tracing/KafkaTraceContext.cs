using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

namespace LedgerService.Worker.Messaging.Kafka.Tracing;

public static class KafkaTraceContext
{
    public static Activity? StartProducerActivity(
        ActivitySource activitySource,
        string operationName,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        var activity = TryParseParent(traceParent, traceState, out var parentContext)
            ? activitySource.StartActivity(operationName, ActivityKind.Producer, parentContext)
            : activitySource.StartActivity(operationName, ActivityKind.Producer);

        AddBaggage(activity, baggage);
        return activity;
    }

    public static bool TryParseParent(string? traceParent, string? traceState, out ActivityContext parentContext)
    {
        parentContext = default;
        return !string.IsNullOrWhiteSpace(traceParent)
            && ActivityContext.TryParse(traceParent, traceState, out parentContext);
    }

    public static void AddPropagationHeaders(
        Headers headers,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        AddIfPresent(headers, KafkaHeaderNames.TraceParent, traceParent);
        AddIfPresent(headers, KafkaHeaderNames.TraceState, traceState);
        AddIfPresent(headers, KafkaHeaderNames.Baggage, baggage);
    }

    public static string? FormatCurrentBaggage()
    {
        var activity = Activity.Current;
        if (activity is null)
            return null;

        var baggage = string.Join(",", activity.Baggage.Select(x => $"{x.Key}={x.Value}"));
        return string.IsNullOrWhiteSpace(baggage) ? null : baggage;
    }

    private static void AddIfPresent(Headers headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            headers.Add(name, Encoding.UTF8.GetBytes(value));
    }

    private static void AddBaggage(Activity? activity, string? baggage)
    {
        if (activity is null || string.IsNullOrWhiteSpace(baggage))
            return;

        foreach (var item in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = item.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == pair.Length - 1)
                continue;

            activity.AddBaggage(pair[..separatorIndex], pair[(separatorIndex + 1)..]);
        }
    }
}
