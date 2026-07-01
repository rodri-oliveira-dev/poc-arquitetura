namespace AuditService.Application.Abstractions.Persistence;

public sealed record AuditRecordSearchCriteria(
    string? MerchantId,
    string? SourceService,
    string? OperationType,
    string? Status,
    string? EntityType,
    string? EntityId,
    DateTimeOffset From,
    DateTimeOffset To,
    int Page,
    int PageSize);
