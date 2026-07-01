namespace BalanceService.Application.Balances.Replay;

public enum FilteredEventReplayItemStatus
{
    Eligible = 0,
    Replayed,
    AlreadyProcessed,
    RejectedInvalidContract,
    RejectedUnsupportedVersion,
    FailedProcessing
}
