using FluentValidation;

namespace AuditService.Application.FunctionalAuditing.GetAuditRecordById;

public sealed class GetAuditRecordByIdQueryValidator : AbstractValidator<GetAuditRecordByIdQuery>
{
    public GetAuditRecordByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}
