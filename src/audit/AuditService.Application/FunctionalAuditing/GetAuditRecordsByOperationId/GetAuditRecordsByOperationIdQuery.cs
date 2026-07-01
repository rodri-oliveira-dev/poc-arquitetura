using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordsByOperationId;

public sealed record GetAuditRecordsByOperationIdQuery(string OperationId)
    : IRequest<IReadOnlyCollection<AuditRecordReadModel>>;
