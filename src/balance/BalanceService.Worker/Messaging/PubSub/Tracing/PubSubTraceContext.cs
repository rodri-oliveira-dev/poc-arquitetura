namespace BalanceService.Worker.Messaging.PubSub.Tracing;

public static class PubSubTraceContext
{
    public static IReadOnlyDictionary<string, string> ReadAttributes(
        IEnumerable<KeyValuePair<string, string>>? attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (attributes is null)
            return result;

        foreach (var attribute in attributes)
            result[attribute.Key] = attribute.Value;

        return result;
    }
}
