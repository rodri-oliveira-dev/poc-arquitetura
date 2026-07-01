namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplayFilter(
    string? EventName,
    string? EventVersion,
    DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredUntil,
    string? MerchantId,
    string? AccountId,
    string? Status);
