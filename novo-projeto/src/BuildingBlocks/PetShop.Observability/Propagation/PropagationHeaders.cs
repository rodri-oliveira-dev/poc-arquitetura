namespace PetShop.Observability.Propagation;

public static class PropagationHeaders
{
    public static void Inject(
        IDictionary<string, string> headers,
        PropagationContextSnapshot context)
    {
        ArgumentNullException.ThrowIfNull(headers);

        SetOrRemove(headers, PropagationHeaderNames.CorrelationId, context.CorrelationId);
        SetOrRemove(headers, PropagationHeaderNames.TenantId, context.TenantId);
        SetOrRemove(headers, PropagationHeaderNames.TraceParent, context.TraceParent);
        SetOrRemove(headers, PropagationHeaderNames.TraceState, context.TraceState);
        SetOrRemove(headers, PropagationHeaderNames.Baggage, context.Baggage);
    }

    public static PropagationContextSnapshot Extract(
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> header in headers)
        {
            if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(header.Value))
            {
                values[header.Key.Trim()] = header.Value.Trim();
            }
        }

        return new PropagationContextSnapshot(
            CorrelationIdNormalizer.Normalize(Get(values, PropagationHeaderNames.CorrelationId)),
            PropagationContextSnapshot.NormalizeOptional(Get(values, PropagationHeaderNames.TenantId)),
            PropagationContextSnapshot.NormalizeOptional(Get(values, PropagationHeaderNames.TraceParent)),
            PropagationContextSnapshot.NormalizeOptional(Get(values, PropagationHeaderNames.TraceState)),
            PropagationContextSnapshot.NormalizeOptional(Get(values, PropagationHeaderNames.Baggage)));
    }

    private static string? Get(IReadOnlyDictionary<string, string> headers, string name)
    {
        return headers.TryGetValue(name, out string? value)
            ? value
            : null;
    }

    private static void SetOrRemove(
        IDictionary<string, string> headers,
        string name,
        string? value)
    {
        string? existingKey = headers.Keys.FirstOrDefault(
            key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(value))
        {
            if (existingKey is not null)
            {
                headers.Remove(existingKey);
            }

            return;
        }

        headers[existingKey ?? name] = value.Trim();
    }
}
