namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplayContract(
    string EventName,
    string EventVersion,
    string? Provider);
