namespace BalanceService.Worker.Messaging.Abstractions;

public sealed record DeadLetterMessage(
    string? OriginalPayload,
    string Source,
    string EventType,
    string Reason,
    string ExceptionType,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> TransportMetadata);
