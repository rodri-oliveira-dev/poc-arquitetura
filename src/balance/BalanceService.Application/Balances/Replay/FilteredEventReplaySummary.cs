namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplaySummary(
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalAlreadyProcessed,
    int TotalEligible,
    int TotalRejected,
    int TotalReplayed);
