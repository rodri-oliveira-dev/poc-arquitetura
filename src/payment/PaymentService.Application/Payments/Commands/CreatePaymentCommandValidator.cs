using FluentValidation;

using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Commands;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Idempotency-Key must be a valid UUID.")
            .MaximumLength(200);

        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId is required.")
            .MaximumLength(MerchantId.MaxLength);

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .PrecisionScale(18, 2, true)
            .WithMessage("Amount must be greater than zero with at most 18 digits and 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Equal(Currency.BrlCode)
            .WithMessage("Only BRL is supported in the payments MVP.");

        RuleFor(x => x.Description)
            .MaximumLength(Payment.DescriptionMaxLength);

        RuleFor(x => x.ExternalReference)
            .MaximumLength(ExternalReference.MaxLength);

        RuleFor(x => x.CorrelationId)
            .MaximumLength(200);
    }
}
