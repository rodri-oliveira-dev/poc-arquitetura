using FluentValidation;
using System.Globalization;

namespace LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

public sealed class CreateLancamentoInputValidator : AbstractValidator<CreateLancamentoInput>
{
    public CreateLancamentoInputValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId is required.")
            .MaximumLength(100);

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => type is "CREDIT" or "DEBIT")
            .WithMessage("Type must be CREDIT or DEBIT.");

        RuleFor(x => x.Amount)
            .NotEmpty()
            .Must(BeValidPositiveAmount)
            .WithMessage("Must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must have 3 characters.");

        RuleFor(x => x.OccurredAt)
            .NotEmpty()
            .Must(BeValidDateTime)
            .WithMessage("OccurredAt must be a valid ISO date-time.");

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.ExternalReference)
            .MaximumLength(150);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .Must(BeValidGuid)
            .WithMessage("Idempotency-Key must be a valid UUID.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .Must(BeValidGuid)
            .WithMessage("X-Correlation-Id must be a valid UUID.");
    }

    private static bool BeValidPositiveAmount(string amount)
        => decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0;

    private static bool BeValidDateTime(string occurredAt)
        => DateTime.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);

    private static bool BeValidGuid(string value)
        => Guid.TryParse(value, out _);
}