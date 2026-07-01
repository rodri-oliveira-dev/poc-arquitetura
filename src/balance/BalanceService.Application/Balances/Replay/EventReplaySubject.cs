namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplaySubject(
    string? MerchantId,
    string? AccountId);
