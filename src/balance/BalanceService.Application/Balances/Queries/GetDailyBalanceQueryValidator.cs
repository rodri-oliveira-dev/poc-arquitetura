using FluentValidation;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetDailyBalanceQueryValidator : AbstractValidator<GetDailyBalanceQuery>
{
    public GetDailyBalanceQueryValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty();
    }
}
