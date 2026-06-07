namespace BalanceService.Application.Balances.Replay;

public sealed record PartialProjectionRebuildResult(
    string RebuildId,
    bool DryRun,
    bool Mutated,
    string FilterDescription,
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalDuplicates,
    int TotalEligible,
    int TotalRejected,
    int TotalRebuilt,
    int TotalDailyBalancesDeleted,
    int TotalProcessedEventsDeleted,
    IReadOnlyList<PartialProjectionRebuildItemResult> Items);
