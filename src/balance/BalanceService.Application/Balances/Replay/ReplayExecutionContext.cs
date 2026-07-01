namespace BalanceService.Application.Balances.Replay;

public sealed record ReplayExecutionContext(
    string OperationId,
    bool DryRun,
    string Reason);
