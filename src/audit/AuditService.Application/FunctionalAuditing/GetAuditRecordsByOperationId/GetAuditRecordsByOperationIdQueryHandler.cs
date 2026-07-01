using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordsByOperationId;

public sealed class GetAuditRecordsByOperationIdQueryHandler(IFunctionalAuditRecordQueryService queryService)
    : IRequestHandler<GetAuditRecordsByOperationIdQuery, IReadOnlyCollection<AuditRecordReadModel>>
{
    private readonly IFunctionalAuditRecordQueryService _queryService = queryService;

    public Task<IReadOnlyCollection<AuditRecordReadModel>> Handle(
        GetAuditRecordsByOperationIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _queryService.GetByOperationIdAsync(request.OperationId, cancellationToken);
    }
}
