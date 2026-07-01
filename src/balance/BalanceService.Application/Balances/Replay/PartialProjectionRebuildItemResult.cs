namespace BalanceService.Application.Balances.Replay;

public sealed record PartialProjectionRebuildItemResult(
    string SourceId,
    string? EventId,
    string EventName,
    string EventVersion,
    PartialProjectionRebuildItemStatus Status,
    string? ErrorMessage);
