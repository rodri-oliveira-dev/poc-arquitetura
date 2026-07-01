using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordById;

public sealed class GetAuditRecordByIdQueryHandler(IFunctionalAuditRecordQueryService queryService)
    : IRequestHandler<GetAuditRecordByIdQuery, AuditRecordReadModel?>
{
    private readonly IFunctionalAuditRecordQueryService _queryService = queryService;

    public Task<AuditRecordReadModel?> Handle(
        GetAuditRecordByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _queryService.GetByIdAsync(request.Id, cancellationToken);
    }
}
