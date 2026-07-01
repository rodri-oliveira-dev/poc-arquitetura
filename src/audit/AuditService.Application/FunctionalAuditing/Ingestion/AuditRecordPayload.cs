namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed record AuditRecordPayload(
    Guid OperationId,
    string SourceService,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    AuditActor? Actor,
    string Status,
    string? Reason,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTimeOffset OccurredAt);
