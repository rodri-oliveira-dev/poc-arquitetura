using FluentValidation;

namespace TransferService.Application.Transferencias.Queries;

public sealed class ObterStatusTransferenciaQueryValidator : AbstractValidator<ObterStatusTransferenciaQuery>
{
    public ObterStatusTransferenciaQueryValidator()
    {
        RuleFor(x => x.TransferenciaId)
            .NotEmpty();

        RuleFor(x => x.AuthorizedMerchantIds)
            .NotNull();
    }
}
