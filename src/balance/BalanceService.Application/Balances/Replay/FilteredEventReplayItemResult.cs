namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplayItemResult(
    string SourceId,
    string? EventId,
    string EventName,
    string EventVersion,
    FilteredEventReplayItemStatus Status,
    string? ErrorMessage);
