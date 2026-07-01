namespace IdentityService.Application.Idempotency;

public sealed record IdempotentOperationResult<TResponse>(
    IdempotentOperationResultKind Kind,
    TResponse? Response,
    int? ResponseStatusCode,
    string? ErrorMessage)
{
    public bool OperationExecutedNow => Kind == IdempotentOperationResultKind.ExecutedNow;

    public bool ResponseRecoveredFromPreviousExecution
        => Kind == IdempotentOperationResultKind.RecoveredFromPreviousExecution;

    public bool IsConflict => Kind == IdempotentOperationResultKind.ConflictingPayload;

    public bool IsInProgress => Kind == IdempotentOperationResultKind.InProgress;
}
