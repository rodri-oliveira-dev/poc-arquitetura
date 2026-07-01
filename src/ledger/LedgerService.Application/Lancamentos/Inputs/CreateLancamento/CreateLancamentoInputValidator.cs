using System.Globalization;

using FluentValidation;

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
            .Must(BeValidDecimal)
            .WithMessage("Amount must be a valid decimal.")
            .Must(HaveSupportedPrecisionAndScale)
            .WithMessage("Amount must have at most 18 digits and 2 decimal places.")
            .Must((input, amount) => BeValidAmountForType(input.Type, amount))
            .WithMessage("Amount não respeita a regra do Type (CREDIT > 0, DEBIT < 0 e nunca 0).");

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.ExternalReference)
            .MaximumLength(150);
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

    private static bool BeValidDecimal(string amount)
        => decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    private static bool HaveSupportedPrecisionAndScale(string amount)
    {
        if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            return false;

        var normalized = amount.Trim();
        if (normalized.StartsWith('-') || normalized.StartsWith('+'))
            normalized = normalized[1..];

        var parts = normalized.Split('.');
        var integerDigits = parts[0].Count(char.IsDigit);
        var decimalDigits = parts.Length == 2 ? parts[1].Count(char.IsDigit) : 0;
        var totalDigits = integerDigits + decimalDigits;

        return parts.Length <= 2 && totalDigits <= 18 && decimalDigits <= 2;
    }
}
