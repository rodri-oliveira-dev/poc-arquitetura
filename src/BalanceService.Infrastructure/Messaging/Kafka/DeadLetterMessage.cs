namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed record DeadLetterMessage(
    string? OriginalPayload,
    string OriginalTopic,
    int OriginalPartition,
    long OriginalOffset,
    IReadOnlyDictionary<string, string> OriginalHeaders,
    string Reason,
    string ExceptionType,
    DateTimeOffset Timestamp);
