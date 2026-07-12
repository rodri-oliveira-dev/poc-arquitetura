using FluentValidation;

namespace PaymentService.Application.Payments.Commands;

public sealed class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Idempotency-Key must be a valid UUID.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ExternalReference)
            .MaximumLength(200);
    }
}
