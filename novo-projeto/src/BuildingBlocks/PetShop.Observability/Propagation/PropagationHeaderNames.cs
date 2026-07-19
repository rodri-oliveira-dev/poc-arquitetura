namespace PetShop.Observability.Propagation;

public static class PropagationHeaderNames
{
    public const string HttpCorrelationId = "X-Correlation-Id";
    public const string CorrelationId = "correlation_id";
    public const string TenantId = "tenant_id";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
    public const string Baggage = "baggage";
}
