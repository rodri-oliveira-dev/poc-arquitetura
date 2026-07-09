using MediatR;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Queries;

public sealed class GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
        : IRequestHandler<GetPaymentByIdQuery, PaymentDetailsResult>
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;

    public async Task<PaymentDetailsResult> Handle(
        GetPaymentByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payment = await _paymentRepository.GetByIdAsync(new PaymentId(request.PaymentId), cancellationToken) ?? throw new NotFoundException("Payment nao encontrado.");

        return !request.AuthorizedMerchantIds.Any(value =>
                string.Equals(value, payment.MerchantId.Value, StringComparison.Ordinal))
            ? throw new ForbiddenException("Token sem autorizacao para o merchant do Payment.")
            : new PaymentDetailsResult(
            payment.PaymentId.Value,
            payment.Status.ToString(),
            payment.MerchantId.Value,
            payment.Amount.Amount,
            payment.Amount.Currency.Code,
            payment.Provider.ToString(),
            payment.Description,
            payment.ExternalReference?.Value,
            payment.ExternalPaymentReference?.Value,
            payment.LedgerEntryReference?.Value,
            payment.ProviderStatus,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.CompletedAt);
    }
}
