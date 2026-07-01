using FluentValidation;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetPeriodBalanceQueryValidator : AbstractValidator<GetPeriodBalanceQuery>
{
    public GetPeriodBalanceQueryValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty();

        RuleFor(x => x)
            .Must(x => x.From <= x.To)
            .WithMessage("From must be less than or equal to To.");
    }
}
