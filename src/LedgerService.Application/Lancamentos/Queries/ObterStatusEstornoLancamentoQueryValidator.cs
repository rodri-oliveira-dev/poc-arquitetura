using FluentValidation;

namespace LedgerService.Application.Lancamentos.Queries;

public sealed class ObterStatusEstornoLancamentoQueryValidator : AbstractValidator<ObterStatusEstornoLancamentoQuery>
{
    public ObterStatusEstornoLancamentoQueryValidator()
    {
        RuleFor(x => x.EstornoId)
            .NotEmpty()
            .WithMessage("EstornoId is required.");

        RuleFor(x => x.AuthorizedMerchantIds)
            .NotEmpty()
            .WithMessage("AuthorizedMerchantIds is required.");
    }
}
