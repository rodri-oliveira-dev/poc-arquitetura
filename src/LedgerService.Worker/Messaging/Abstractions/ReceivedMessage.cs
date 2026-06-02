namespace LedgerService.Worker.Messaging.Abstractions;

public sealed record ReceivedMessage(
    string Payload,
    string EventType,
    string? EventId,
    string? CorrelationId,
    string? TraceParent,
    string? TraceState,
    string? Baggage,
    string? OrderingKey,
    IReadOnlyDictionary<string, string> Attributes,
    TransportMessageContext Transport);

public sealed record TransportMessageContext(
    string Provider,
    string Source,
    string? Partition,
    string? Offset,
    string? DeliveryAttempt,
    IReadOnlyDictionary<string, string> Metadata);
