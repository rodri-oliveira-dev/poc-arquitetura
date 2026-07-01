namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceIdentity(
    string? AccountId,
    string MerchantId,
    DateOnly Date,
    string Currency);
