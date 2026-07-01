namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildEvaluationSummary(
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalDuplicates,
    int TotalEligible,
    int TotalRejected);
