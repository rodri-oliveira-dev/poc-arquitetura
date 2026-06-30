namespace IdentityService.Application.Idempotency;

public sealed class IdempotentOperationRequest<TResponse>
{
    public IdempotentOperationRequest(
        string operationName,
        string idempotencyKey,
        string requestHash,
        int responseStatusCode,
        TimeSpan timeToLive,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        Func<TResponse, Guid?>? resourceIdSelector = null,
        TimeSpan? processingLockDuration = null,
        Func<Exception, CancellationToken, Task<string?>>? onPersistenceFailureAsync = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name is required.", nameof(operationName));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("Request hash is required.", nameof(requestHash));

        if (responseStatusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode), "Response status code must be valid HTTP status.");

        if (timeToLive <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time to live must be positive.");

        ArgumentNullException.ThrowIfNull(executeAsync);

        OperationName = operationName;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        ResponseStatusCode = responseStatusCode;
        TimeToLive = timeToLive;
        ExecuteAsync = executeAsync;
        ResourceIdSelector = resourceIdSelector;
        ProcessingLockDuration = processingLockDuration;
        OnPersistenceFailureAsync = onPersistenceFailureAsync;
    }

    public string OperationName
    {
        get;
    }

    public string IdempotencyKey
    {
        get;
    }

    public string RequestHash
    {
        get;
    }

    public int ResponseStatusCode
    {
        get;
    }

    public TimeSpan TimeToLive
    {
        get;
    }

    public Func<CancellationToken, Task<TResponse>> ExecuteAsync
    {
        get;
    }

    public Func<TResponse, Guid?>? ResourceIdSelector
    {
        get;
    }

    public TimeSpan? ProcessingLockDuration
    {
        get;
    }

    public Func<Exception, CancellationToken, Task<string?>>? OnPersistenceFailureAsync
    {
        get;
    }
}
