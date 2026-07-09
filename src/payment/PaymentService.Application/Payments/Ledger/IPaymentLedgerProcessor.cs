namespace PaymentService.Application.Payments.Ledger;

public interface IPaymentLedgerProcessor
{
    Task<PaymentLedgerProcessorResult> ProcessBatchAsync(
        int batchSize,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken);
}
