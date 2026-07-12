using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Abstractions.Persistence;

public interface IPaymentInboxRepository
{
    Task<PaymentInboxStoreResult> StoreAsync(
        PaymentInboxMessage message,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentInboxMessage>> ClaimEligibleAsync(
        int batchSize,
        DateTimeOffset now,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken);

    Task<PaymentInboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PaymentInboxStatus> MarkFailedProcessingAttemptAsync(
        Guid id,
        int maxRetryCount,
        DateTimeOffset now,
        DateTimeOffset nextRetryAt,
        string lastError,
        CancellationToken cancellationToken);

    Task<int> CountBacklogAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
