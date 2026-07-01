namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildMutationSummary(
    int TotalRebuilt,
    int TotalDailyBalancesDeleted,
    int TotalProcessedEventsDeleted);
