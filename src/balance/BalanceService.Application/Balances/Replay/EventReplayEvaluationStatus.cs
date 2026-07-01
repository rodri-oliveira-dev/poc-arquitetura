namespace BalanceService.Application.Balances.Replay;

public enum EventReplayEvaluationStatus
{
    Eligible = 0,
    AlreadyProcessed,
    InvalidContract,
    UnsupportedVersion
}
