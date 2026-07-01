using AuditService.Application.FunctionalAuditing.ReadModels;

using MediatR;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordById;

public sealed record GetAuditRecordByIdQuery(Guid Id) : IRequest<AuditRecordReadModel?>;
