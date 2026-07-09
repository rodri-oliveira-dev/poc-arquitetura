namespace PaymentService.Worker.Options;

public sealed class PaymentLedgerWorkerOptions
{
    public const string SectionName = "PaymentService:LedgerWorker";

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; init; } = 20;

    public int MaxRetryCount { get; init; } = 5;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan ProcessingLeaseTimeout { get; init; } = TimeSpan.FromMinutes(1);
}
