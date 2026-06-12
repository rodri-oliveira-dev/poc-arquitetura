namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceCounters(
    int EventsAnalyzed,
    int InvalidEvents,
    int DuplicateEventsIgnored);
