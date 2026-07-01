namespace BalanceService.Application.Balances.Replay;

public enum ProjectionRebuildEventItemStatus
{
    Eligible = 0,
    RejectedInvalidContract = 1,
    RejectedUnsupportedVersion = 2,
    DuplicateInBatch = 3
}
