namespace BalanceService.Application.Balances.Replay;

public enum PartialProjectionRebuildItemStatus
{
    Eligible = 0,
    Rebuilt,
    DuplicateInBatch,
    RejectedInvalidContract,
    RejectedUnsupportedVersion,
    SkippedConcurrentDuplicate
}
