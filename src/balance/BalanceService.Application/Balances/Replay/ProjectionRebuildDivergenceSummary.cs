namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceSummary(
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalDuplicates,
    int TotalCompared,
    bool HasDivergences);
