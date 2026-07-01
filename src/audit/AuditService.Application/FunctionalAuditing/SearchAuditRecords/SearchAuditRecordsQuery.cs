using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.SearchAuditRecords;

public sealed record SearchAuditRecordsQuery(
    string? MerchantId,
    string? SourceService,
    string? OperationType,
    string? Status,
    string? EntityType,
    string? EntityId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = SearchAuditRecordsQuery.DefaultPage,
    int PageSize = SearchAuditRecordsQuery.DefaultPageSize)
    : IRequest<PagedResult<AuditRecordReadModel>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
    public const int MaxIntervalDays = 31;
}
