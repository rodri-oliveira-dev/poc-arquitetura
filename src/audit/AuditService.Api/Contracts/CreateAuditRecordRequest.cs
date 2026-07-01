namespace AuditService.Api.Contracts;

public sealed record CreateAuditRecordRequest(
    Guid OperationId,
    Guid? CorrelationId,
    string SourceService,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    CreateAuditRecordActorRequest? Actor,
    string Status,
    string? Reason,
    Dictionary<string, string>? Metadata,
    DateTimeOffset OccurredAt);
