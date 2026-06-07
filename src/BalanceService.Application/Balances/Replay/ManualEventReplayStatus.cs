namespace BalanceService.Application.Balances.Replay;

public enum ManualEventReplayStatus
{
    Replayed = 0,
    SkippedAlreadyProcessed,
    RejectedInvalidContract,
    RejectedUnsupportedVersion,
    FailedProcessing
}
