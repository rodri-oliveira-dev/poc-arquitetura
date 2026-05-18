using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public static class KafkaTraceContext
{
    public static IReadOnlyDictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
            return result;

        foreach (var header in headers)
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

        return result;
    }

    public static Activity? StartConsumerActivity(
        ActivitySource activitySource,
        string operationName,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        var activity = TryParseParent(traceParent, traceState, out var parentContext)
            ? activitySource.StartActivity(operationName, ActivityKind.Consumer, parentContext)
            : activitySource.StartActivity(operationName, ActivityKind.Consumer);

        AddBaggage(activity, baggage);
        return activity;
    }

    public static void CopyHeaderIfPresent(IReadOnlyDictionary<string, string> source, Headers target, string name)
    {
        if (source.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            target.Add(name, Encoding.UTF8.GetBytes(value));
    }

    private static bool TryParseParent(string? traceParent, string? traceState, out ActivityContext parentContext)
    {
        parentContext = default;
        return !string.IsNullOrWhiteSpace(traceParent)
            && ActivityContext.TryParse(traceParent, traceState, out parentContext);
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
