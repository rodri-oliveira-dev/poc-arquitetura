using FluentValidation;

namespace PaymentService.Application.Payments.Queries;

public sealed class GetPaymentByIdQueryValidator : AbstractValidator<GetPaymentByIdQuery>
{
    public GetPaymentByIdQueryValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.AuthorizedMerchantIds).NotNull();
    }
}
