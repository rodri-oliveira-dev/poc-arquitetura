namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceItem(
    string? AccountId,
    string MerchantId,
    DateOnly Date,
    string Currency,
    decimal CurrentBalance,
    decimal RebuiltBalance,
    decimal Difference,
    int EventsAnalyzed,
    int InvalidEvents,
    int DuplicateEventsIgnored);
