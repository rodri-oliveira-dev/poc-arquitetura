namespace BalanceService.Application.Balances.Replay;

public sealed record PartialProjectionRebuildFilter(
    string MerchantId,
    DateTimeOffset OccurredFrom,
    DateTimeOffset OccurredUntil,
    string? EventVersion = "v2");
