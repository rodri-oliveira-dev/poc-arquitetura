using System.Diagnostics;

namespace PetShop.Observability.Propagation;

public readonly record struct PropagationContextSnapshot(
    string? CorrelationId,
    string? TenantId,
    string? TraceParent,
    string? TraceState,
    string? Baggage)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(CorrelationId) &&
        string.IsNullOrWhiteSpace(TenantId) &&
        string.IsNullOrWhiteSpace(TraceParent) &&
        string.IsNullOrWhiteSpace(TraceState) &&
        string.IsNullOrWhiteSpace(Baggage);

    public static PropagationContextSnapshot CaptureCurrent(
        string? correlationId = null,
        string? tenantId = null)
    {
        Activity? activity = Activity.Current;

        return new PropagationContextSnapshot(
            CorrelationIdNormalizer.Normalize(
                correlationId ?? activity?.GetBaggageItem(PropagationHeaderNames.CorrelationId)),
            NormalizeOptional(
                tenantId ?? activity?.GetBaggageItem(PropagationHeaderNames.TenantId)),
            NormalizeOptional(activity?.Id),
            NormalizeOptional(activity?.TraceStateString),
            BaggageCodec.Format(activity?.Baggage));
    }

    internal static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
