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
            .Must((input, amount) => BeValidAmountForType(input.Type, amount))
            .WithMessage("Amount não respeita a regra do Type (CREDIT > 0, DEBIT < 0 e nunca 0). ");

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

    private static bool BeValidAmountForType(string type, string amount)
    {
        if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return false;

        if (value == 0)
            return false;

        // type já deve ter sido normalizado (Bind) para canônico.
        return type switch
        {
            "CREDIT" => value > 0,
            "DEBIT" => value < 0,
            _ => false
        };
    }

    private static bool BeValidDateTime(string occurredAt)
        => DateTime.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);

    private static bool BeValidGuid(string value)
        => Guid.TryParse(value, out _);
}