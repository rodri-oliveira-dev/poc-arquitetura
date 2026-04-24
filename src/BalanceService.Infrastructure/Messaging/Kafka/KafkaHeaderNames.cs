namespace BalanceService.Infrastructure.Messaging.Kafka;

public static class KafkaHeaderNames
{
    public const string EventType = "event_type";
    public const string EventId = "event_id";
    public const string CorrelationId = "correlation_id";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
    public const string Baggage = "baggage";
}
