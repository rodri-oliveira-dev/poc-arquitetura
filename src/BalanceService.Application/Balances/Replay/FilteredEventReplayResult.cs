namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplayResult(
    string ReplayId,
    bool DryRun,
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalAlreadyProcessed,
    int TotalEligible,
    int TotalRejected,
    int TotalReplayed,
    IReadOnlyList<FilteredEventReplayItemResult> Items);
