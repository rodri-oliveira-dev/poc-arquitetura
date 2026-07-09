using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Abstractions.Persistence;

public interface IPaymentInboxRepository
{
    Task<PaymentInboxStoreResult> StoreAsync(
        PaymentInboxMessage message,
        CancellationToken cancellationToken);
}
