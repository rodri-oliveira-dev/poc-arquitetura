using FluentValidation;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class SolicitarReprocessamentoLancamentosCommandValidator
    : AbstractValidator<SolicitarReprocessamentoLancamentosCommand>
{
    public const int MaxPeriodoDias = 31;

    public SolicitarReprocessamentoLancamentosCommandValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId is required.")
            .MaximumLength(100);

        RuleFor(x => x.DataInicial)
            .NotEmpty()
            .WithMessage("DataInicial is required.");

        RuleFor(x => x.DataFinal)
            .NotEmpty()
            .WithMessage("DataFinal is required.")
            .GreaterThanOrEqualTo(x => x.DataInicial)
            .WithMessage("DataFinal must be greater than or equal to DataInicial.")
            .Must((command, dataFinal) => CountInclusiveDays(command.DataInicial, dataFinal) <= MaxPeriodoDias)
            .WithMessage($"Periodo de reprocessamento deve ter no maximo {MaxPeriodoDias} dias.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("Motivo is required.")
            .MinimumLength(10)
            .WithMessage("Motivo must have at least 10 characters.")
            .MaximumLength(500);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Idempotency-Key must be a valid UUID.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("CorrelationId must be a valid UUID.");
    }

    private static int CountInclusiveDays(DateOnly dataInicial, DateOnly dataFinal)
        => dataFinal.DayNumber - dataInicial.DayNumber + 1;
}
