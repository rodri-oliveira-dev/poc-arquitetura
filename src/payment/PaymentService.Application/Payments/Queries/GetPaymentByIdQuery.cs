using MediatR;

namespace PaymentService.Application.Payments.Queries;

public sealed record GetPaymentByIdQuery(
    Guid PaymentId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<PaymentDetailsResult>;
