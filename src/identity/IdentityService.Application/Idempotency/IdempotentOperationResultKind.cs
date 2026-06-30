namespace IdentityService.Application.Idempotency;

public enum IdempotentOperationResultKind
{
    ExecutedNow = 1,
    RecoveredFromPreviousExecution = 2,
    ConflictingPayload = 3,
    InProgress = 4
}
