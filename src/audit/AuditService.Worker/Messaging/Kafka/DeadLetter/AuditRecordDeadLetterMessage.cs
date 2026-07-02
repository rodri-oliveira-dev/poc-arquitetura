namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal sealed record AuditRecordDeadLetterMessage(
    Guid? EventId,
    Guid? CorrelationId,
    string OriginalTopic,
    int OriginalPartition,
    long OriginalOffset,
    string FailureReason,
    string FailureCategory,
    DateTimeOffset OccurredAt,
    string PayloadSha256);
