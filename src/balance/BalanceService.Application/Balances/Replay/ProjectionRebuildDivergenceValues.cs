namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceValues(
    decimal CurrentBalance,
    decimal RebuiltBalance,
    decimal Difference);
