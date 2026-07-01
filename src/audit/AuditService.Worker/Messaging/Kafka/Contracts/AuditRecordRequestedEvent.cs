namespace AuditService.Worker.Messaging.Kafka.Contracts;

internal sealed record AuditRecordRequestedEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    string SourceService,
    Guid OperationId,
    Guid? CorrelationId,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    AuditRecordRequestedActor? Actor,
    string Status,
    string? Reason,
    IReadOnlyDictionary<string, string>? Metadata);
