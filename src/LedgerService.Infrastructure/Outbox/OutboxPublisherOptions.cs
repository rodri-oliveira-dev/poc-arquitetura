namespace LedgerService.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "Outbox:Publisher";

    public int PollingIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 50;
    public int MaxParallelism { get; init; } = 4;

    public int MaxAttempts { get; init; } = 10;
    public int BaseBackoffSeconds { get; init; } = 5;

    public int LockDurationSeconds { get; init; } = 60;
}
