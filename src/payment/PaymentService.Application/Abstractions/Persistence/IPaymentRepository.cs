using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken);

    Task<Payment?> GetByIdForUpdateAsync(PaymentId paymentId, CancellationToken cancellationToken);

    Task<Payment?> GetByProviderReferenceForUpdateAsync(
        PaymentProvider provider,
        ExternalPaymentReference externalPaymentReference,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ClaimLedgerIntegrationAsync(
        int batchSize,
        DateTimeOffset now,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
