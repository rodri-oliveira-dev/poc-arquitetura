namespace AuditService.Api.Contracts;

public sealed record AuditRecordResponse(
    Guid Id,
    string OperationId,
    string? CorrelationId,
    string SourceService,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    AuditRecordActorResponse? Actor,
    string Status,
    string? Reason,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt);
