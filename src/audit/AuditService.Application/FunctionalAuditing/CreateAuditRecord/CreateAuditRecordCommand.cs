using MediatR;

namespace AuditService.Application.FunctionalAuditing.CreateAuditRecord;

public sealed record CreateAuditRecordCommand(
    Guid OperationId,
    Guid? CorrelationId,
    string? IdempotencyKey,
    string SourceService,
    string OperationType,
    string? EntityType,
    string? EntityId,
    string? MerchantId,
    CreateAuditRecordActor? Actor,
    string Status,
    string? Reason,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTimeOffset OccurredAt,
    Guid? SourceEventId = null) : IRequest<CreateAuditRecordResult>;
