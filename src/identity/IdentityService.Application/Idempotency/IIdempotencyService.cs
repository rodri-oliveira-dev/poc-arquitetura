namespace IdentityService.Application.Idempotency;

public interface IIdempotencyService
{
    Task<IdempotentOperationResult<TResponse>> ExecuteAsync<TResponse>(
        IdempotentOperationRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
