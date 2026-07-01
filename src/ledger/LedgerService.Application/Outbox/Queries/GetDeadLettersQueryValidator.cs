using FluentValidation;

namespace LedgerService.Application.Outbox.Queries;

public sealed class GetDeadLettersQueryValidator : AbstractValidator<GetDeadLettersQuery>
{
    public GetDeadLettersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
