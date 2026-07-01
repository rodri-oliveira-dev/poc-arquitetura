namespace AuditService.Application.FunctionalAuditing.ReadModels;

public sealed record AuditRecordReadModel(
    Guid Id,
    string OperationId,
    string? CorrelationId,
    string SourceService,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    AuditRecordActorReadModel? Actor,
    string Status,
    string? Reason,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt);
