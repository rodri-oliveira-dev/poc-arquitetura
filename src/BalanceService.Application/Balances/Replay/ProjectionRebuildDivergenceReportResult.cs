namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceReportResult(
    string ReportId,
    bool Mutated,
    string FilterDescription,
    int TotalFound,
    int TotalValid,
    int TotalInvalid,
    int TotalDuplicates,
    int TotalCompared,
    bool HasDivergences,
    IReadOnlyList<ProjectionRebuildDivergenceItem> Items,
    IReadOnlyList<ProjectionRebuildEventItemResult> Events);
