namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildEventItemResult(
    string SourceId,
    string? EventId,
    string EventName,
    string EventVersion,
    string? AccountId,
    ProjectionRebuildEventItemStatus Status,
    string? ErrorMessage);
