using FluentValidation;

namespace TransferService.Application.Transferencias.Commands;

public sealed class SolicitarTransferenciaCommandValidator : AbstractValidator<SolicitarTransferenciaCommand>
{
    public SolicitarTransferenciaCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.")
            .MaximumLength(200);

        RuleFor(x => x.SourceMerchantId)
            .NotEmpty()
            .WithMessage("SourceMerchantId is required.")
            .MaximumLength(100);

        RuleFor(x => x.DestinationMerchantId)
            .NotEmpty()
            .WithMessage("DestinationMerchantId is required.")
            .MaximumLength(100);

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .PrecisionScale(18, 2, true)
            .WithMessage("Amount must be greater than zero with at most 18 digits and 2 decimal places.");

        RuleFor(x => x)
            .Must(x => !string.Equals(
                Normalize(x.SourceMerchantId),
                Normalize(x.DestinationMerchantId),
                StringComparison.Ordinal))
            .WithMessage("SourceMerchantId nao pode ser igual ao DestinationMerchantId.");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(200);
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
