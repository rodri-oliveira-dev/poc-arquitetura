using IdentityService.Application.Idempotency.Ports;

namespace IdentityService.Application.Idempotency;

public sealed class IdempotencyService(
    IIdempotencyRepository records,
    IIdempotencyResponseSerializer serializer,
    TimeProvider timeProvider) : IIdempotencyService
{
    public async Task<IdempotentOperationResult<TResponse>> ExecuteAsync<TResponse>(
        IdempotentOperationRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await records.GetByOperationAndKeyAsync(
            request.OperationName,
            request.IdempotencyKey,
            cancellationToken);

        if (existing is not null)
            return HandleExistingRecord(existing, request);

        var now = GetUtcNow();
        var record = IdempotencyRecord.StartProcessing(
            request.OperationName,
            request.IdempotencyKey,
            request.RequestHash,
            now,
            now.Add(request.TimeToLive),
            request.ProcessingLockDuration is null ? null : now.Add(request.ProcessingLockDuration.Value));

        await records.AddAsync(record, cancellationToken);
        await records.SaveChangesAsync(cancellationToken);

        try
        {
            var response = await request.ExecuteAsync(cancellationToken);
            var responseBody = serializer.Serialize(response);

            record.MarkCompleted(
                request.ResponseStatusCode,
                responseBody,
                request.ResourceIdSelector?.Invoke(response),
                GetUtcNow());

            await records.SaveChangesAsync(cancellationToken);

            return new IdempotentOperationResult<TResponse>(
                IdempotentOperationResultKind.ExecutedNow,
                response,
                request.ResponseStatusCode,
                null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            record.MarkFailed(exception.Message, GetUtcNow());
            await TrySaveFailureAsync(cancellationToken);

            throw;
        }
    }

    private IdempotentOperationResult<TResponse> HandleExistingRecord<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request)
    {
        return !string.Equals(record.RequestHash, request.RequestHash, StringComparison.Ordinal)
            ? new IdempotentOperationResult<TResponse>(
                IdempotentOperationResultKind.ConflictingPayload,
                default,
                null,
                "Idempotency key already used with a different logical payload.")
            : record.Status switch
            {
                IdempotencyStatus.Completed => ReplayCompleted<TResponse>(record),
                IdempotencyStatus.Processing => InProgress<TResponse>("Idempotency key is still processing."),
                IdempotencyStatus.Failed => InProgress<TResponse>(
                    "Previous idempotent operation did not complete successfully."),
                IdempotencyStatus.Expired => InProgress<TResponse>(
                    "Idempotency key is expired and cannot be replayed by this service."),
                _ => throw new InvalidOperationException($"Unsupported idempotency status '{record.Status}'.")
            };
    }

    private IdempotentOperationResult<TResponse> ReplayCompleted<TResponse>(IdempotencyRecord record)
    {
        if (record.ResponseStatusCode is null)
            throw new InvalidOperationException("Completed idempotency record has no response status code.");

        if (string.IsNullOrWhiteSpace(record.ResponseBody))
            throw new InvalidOperationException("Completed idempotency record has no response body.");

        var response = serializer.Deserialize<TResponse>(record.ResponseBody);

        return new IdempotentOperationResult<TResponse>(
            IdempotentOperationResultKind.RecoveredFromPreviousExecution,
            response,
            record.ResponseStatusCode.Value,
            null);
    }

    private static IdempotentOperationResult<TResponse> InProgress<TResponse>(string message)
        => new(IdempotentOperationResultKind.InProgress, default, null, message);

    private async Task TrySaveFailureAsync(CancellationToken cancellationToken)
    {
        try
        {
            await records.SaveChangesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Failure persistence must not mask the original operation exception.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
        }
    }

    private DateTime GetUtcNow()
        => timeProvider.GetUtcNow().UtcDateTime;
}
