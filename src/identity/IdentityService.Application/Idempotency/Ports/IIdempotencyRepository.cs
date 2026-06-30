namespace IdentityService.Application.Idempotency.Ports;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
        string operationName,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddProcessingAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<int> SaveFailureAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
}
