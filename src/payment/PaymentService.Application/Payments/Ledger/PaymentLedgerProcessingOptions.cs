namespace PaymentService.Application.Payments.Ledger;

public sealed class PaymentLedgerProcessingOptions
{
    public int MaxRetryCount { get; init; } = 5;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);
}
