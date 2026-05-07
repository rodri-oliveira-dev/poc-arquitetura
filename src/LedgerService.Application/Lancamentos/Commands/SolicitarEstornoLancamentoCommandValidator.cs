using FluentValidation;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class SolicitarEstornoLancamentoCommandValidator : AbstractValidator<SolicitarEstornoLancamentoCommand>
{
    public SolicitarEstornoLancamentoCommandValidator()
    {
        RuleFor(x => x.LancamentoId)
            .NotEmpty()
            .WithMessage("LancamentoId is required.");

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
}
