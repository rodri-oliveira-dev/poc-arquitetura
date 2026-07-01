using AuditService.Domain.FunctionalAuditing;

using FluentValidation;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordsByOperationId;

public sealed class GetAuditRecordsByOperationIdQueryValidator
    : AbstractValidator<GetAuditRecordsByOperationIdQuery>
{
    public GetAuditRecordsByOperationIdQueryValidator()
    {
        RuleFor(x => x.OperationId)
            .NotEmpty()
            .MaximumLength(FunctionalAuditRecord.OperationIdMaxLength);
    }
}
