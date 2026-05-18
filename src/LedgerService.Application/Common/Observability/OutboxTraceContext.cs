using System.Diagnostics;

namespace LedgerService.Application.Common.Observability;

public readonly record struct OutboxTraceContext(string? TraceParent, string? TraceState, string? Baggage)
{
    public static OutboxTraceContext CaptureCurrent()
    {
        var activity = Activity.Current;
        if (activity is null)
            return default;

        var baggage = string.Join(",", activity.Baggage.Select(x => $"{x.Key}={x.Value}"));

        return new OutboxTraceContext(
            string.IsNullOrWhiteSpace(activity.Id) ? null : activity.Id,
            string.IsNullOrWhiteSpace(activity.TraceStateString) ? null : activity.TraceStateString,
            string.IsNullOrWhiteSpace(baggage) ? null : baggage);
    }
}
