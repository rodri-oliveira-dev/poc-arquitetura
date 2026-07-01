using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.SearchAuditRecords;

public sealed class SearchAuditRecordsQueryHandler(IFunctionalAuditRecordQueryService queryService)
    : IRequestHandler<SearchAuditRecordsQuery, PagedResult<AuditRecordReadModel>>
{
    private readonly IFunctionalAuditRecordQueryService _queryService = queryService;

    public Task<PagedResult<AuditRecordReadModel>> Handle(
        SearchAuditRecordsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var criteria = new AuditRecordSearchCriteria(
            request.MerchantId,
            request.SourceService,
            request.OperationType,
            request.Status,
            request.EntityType,
            request.EntityId,
            request.From!.Value,
            request.To!.Value,
            request.Page,
            request.PageSize);

        return _queryService.SearchAsync(criteria, cancellationToken);
    }
}
