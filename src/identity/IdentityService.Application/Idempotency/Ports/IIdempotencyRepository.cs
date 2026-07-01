namespace IdentityService.Application.Idempotency.Ports;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
        string operationName,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddProcessingAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

    Task<IdempotencyRecord?> TryClaimExpiredForProcessingAsync(
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime nowUtc,
        DateTime expiresAtUtc,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken = default);

    Task<IdempotencyRecord?> TryClaimFailedForRetryAsync(
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime nowUtc,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<int> SaveFailureAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
}
