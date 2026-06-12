namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplaySourcePosition(
    string SourceId,
    DateTimeOffset OccurredAt,
    string? Status);
