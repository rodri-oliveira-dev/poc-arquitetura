namespace IdentityService.Application.Idempotency;

public static class IdempotencyFailureStage
{
    public const string BeforeExternalSideEffect = nameof(BeforeExternalSideEffect);
    public const string AfterIdentityProviderCompensated = nameof(AfterIdentityProviderCompensated);
    public const string AfterIdentityProviderCompensationFailed = nameof(AfterIdentityProviderCompensationFailed);
    public const string AfterLocalPersistenceConfirmed = nameof(AfterLocalPersistenceConfirmed);
    public const string ProcessingLockExpired = nameof(ProcessingLockExpired);
}
