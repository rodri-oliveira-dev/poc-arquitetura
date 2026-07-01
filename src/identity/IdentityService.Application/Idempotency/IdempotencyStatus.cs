namespace IdentityService.Application.Idempotency;

public enum IdempotencyStatus
{
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Expired = 4
}
