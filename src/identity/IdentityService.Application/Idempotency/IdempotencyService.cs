using IdentityService.Application.Idempotency.Ports;

using Microsoft.Extensions.Logging;

namespace IdentityService.Application.Idempotency;

public sealed partial class IdempotencyService(
    IIdempotencyRepository records,
    IIdempotencyResponseSerializer serializer,
    TimeProvider timeProvider,
    ILogger<IdempotencyService> logger) : IIdempotencyService
{
    public async Task<IdempotentOperationResult<TResponse>> ExecuteAsync<TResponse>(
        IdempotentOperationRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var idempotencyKeyHash = HashKeyForLog(request.IdempotencyKey);
        var now = GetUtcNow();
        var record = IdempotencyRecord.StartProcessing(
            request.OperationName,
            request.IdempotencyKey,
            request.RequestHash,
            now,
            now.Add(request.TimeToLive),
            request.ProcessingLockDuration is null ? null : now.Add(request.ProcessingLockDuration.Value));

        if (!await records.TryAddProcessingAsync(record, cancellationToken))
        {
            LogConcurrentReservationDetected(logger, request.OperationName, idempotencyKeyHash);

            var existing = await records.GetByOperationAndKeyAsync(
                request.OperationName,
                request.IdempotencyKey,
                cancellationToken)
                ?? throw new InvalidOperationException("Concurrent idempotency record was not found after unique constraint conflict.");

            return await HandleExistingRecordAsync(existing, request, idempotencyKeyHash, cancellationToken);
        }

        LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, "New", nameof(IdempotencyStatus.Processing));

        try
        {
            var response = await request.ExecuteAsync(cancellationToken);
            var responseBody = serializer.Serialize(response);

            record.MarkCompleted(
                request.ResponseStatusCode,
                responseBody,
                request.ResourceIdSelector?.Invoke(response),
                GetUtcNow());

            LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Processing), nameof(IdempotencyStatus.Completed));
            await records.SaveChangesAsync(cancellationToken);

            return new IdempotentOperationResult<TResponse>(
                IdempotentOperationResultKind.ExecutedNow,
                response,
                request.ResponseStatusCode,
                null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var failureStage = IdempotencyFailureMetadata.GetFailureStage(exception);
            if (failureStage is null && record.Status == IdempotencyStatus.Completed)
            {
                failureStage = request.OnPersistenceFailureAsync is null
                    ? IdempotencyFailureStage.BeforeExternalSideEffect
                    : await request.OnPersistenceFailureAsync(exception, cancellationToken);
            }

            record.MarkFailed(
                failureStage ?? IdempotencyFailureStage.BeforeExternalSideEffect,
                exception.Message,
                GetUtcNow());

            LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Processing), nameof(IdempotencyStatus.Failed));
            await TrySaveFailureAsync(record, cancellationToken);

            throw;
        }
    }

    private async Task<IdempotentOperationResult<TResponse>> HandleExistingRecordAsync<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        if (record.ExpiresAtUtc <= now)
            return await RestartExpiredRecordAsync(record, request, idempotencyKeyHash, now, cancellationToken);

        if (!string.Equals(record.RequestHash, request.RequestHash, StringComparison.Ordinal))
        {
            LogPayloadHashConflict(logger, request.OperationName, idempotencyKeyHash);

            return new IdempotentOperationResult<TResponse>(
                IdempotentOperationResultKind.ConflictingPayload,
                default,
                null,
                "Idempotency key already used with a different logical payload.");
        }

        return record.Status switch
        {
            IdempotencyStatus.Completed => ReplayCompleted(record, request, idempotencyKeyHash),
            IdempotencyStatus.Processing => await HandleProcessingRecordAsync(record, request, idempotencyKeyHash, cancellationToken),
            IdempotencyStatus.Failed when CanRetry(record) => await RetryFailedRecordAsync(record, request, idempotencyKeyHash, cancellationToken),
            IdempotencyStatus.Failed => InProgress<TResponse>(
                "Previous idempotent operation did not complete successfully and cannot be retried automatically."),
            IdempotencyStatus.Expired => InProgress<TResponse>(
                "Idempotency key is expired and cannot be replayed by this service."),
            _ => throw new InvalidOperationException($"Unsupported idempotency status '{record.Status}'.")
        };
    }

    private async Task<IdempotentOperationResult<TResponse>> HandleProcessingRecordAsync<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        if (record.LockedUntilUtc is not null && record.LockedUntilUtc <= now)
        {
            record.MarkFailed(
                IdempotencyFailureStage.ProcessingLockExpired,
                "Processing lock expired before completion.",
                now);

            LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Processing), nameof(IdempotencyStatus.Failed));
            await records.SaveFailureAsync(record, cancellationToken);

            return InProgress<TResponse>(
                "Previous idempotent operation lock expired and requires operational recovery before retry.");
        }

        LogOperationInProgress(logger, request.OperationName, idempotencyKeyHash);
        return InProgress<TResponse>("Idempotency key is still processing.");
    }

    private async Task<IdempotentOperationResult<TResponse>> RetryFailedRecordAsync<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        var claimed = await records.TryClaimFailedForRetryAsync(
            request.OperationName,
            request.IdempotencyKey,
            request.RequestHash,
            now,
            request.ProcessingLockDuration is null ? null : now.Add(request.ProcessingLockDuration.Value),
            cancellationToken);

        if (claimed is null)
        {
            LogOperationInProgress(logger, request.OperationName, idempotencyKeyHash);
            return InProgress<TResponse>("Idempotency key is being retried by another request.");
        }

        record = claimed;

        LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Failed), nameof(IdempotencyStatus.Processing));
        return await ExecuteClaimedProcessingRecordAsync(record, request, idempotencyKeyHash, cancellationToken);
    }

    private async Task<IdempotentOperationResult<TResponse>> RestartExpiredRecordAsync<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var fromStatus = record.Status.ToString();
        var claimed = await records.TryClaimExpiredForProcessingAsync(
            request.OperationName,
            request.IdempotencyKey,
            request.RequestHash,
            now,
            now.Add(request.TimeToLive),
            request.ProcessingLockDuration is null ? null : now.Add(request.ProcessingLockDuration.Value),
            cancellationToken);

        if (claimed is null)
        {
            LogOperationInProgress(logger, request.OperationName, idempotencyKeyHash);
            return InProgress<TResponse>("Expired idempotency key is being reused by another request.");
        }

        LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, fromStatus, nameof(IdempotencyStatus.Processing));
        return await ExecuteClaimedProcessingRecordAsync(claimed, request, idempotencyKeyHash, cancellationToken);
    }

    private async Task<IdempotentOperationResult<TResponse>> ExecuteClaimedProcessingRecordAsync<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await request.ExecuteAsync(cancellationToken);
            record.MarkCompleted(
                request.ResponseStatusCode,
                serializer.Serialize(response),
                request.ResourceIdSelector?.Invoke(response),
                GetUtcNow());

            LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Processing), nameof(IdempotencyStatus.Completed));
            await records.SaveChangesAsync(cancellationToken);

            return new IdempotentOperationResult<TResponse>(
                IdempotentOperationResultKind.ExecutedNow,
                response,
                request.ResponseStatusCode,
                null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var failureStage = IdempotencyFailureMetadata.GetFailureStage(exception);
            if (failureStage is null && record.Status == IdempotencyStatus.Completed)
            {
                failureStage = request.OnPersistenceFailureAsync is null
                    ? IdempotencyFailureStage.BeforeExternalSideEffect
                    : await request.OnPersistenceFailureAsync(exception, cancellationToken);
            }

            record.MarkFailed(
                failureStage ?? IdempotencyFailureStage.BeforeExternalSideEffect,
                exception.Message,
                GetUtcNow());
            LogStatusTransition(logger, request.OperationName, idempotencyKeyHash, nameof(IdempotencyStatus.Processing), nameof(IdempotencyStatus.Failed));
            await TrySaveFailureAsync(record, cancellationToken);

            throw;
        }
    }

    private IdempotentOperationResult<TResponse> ReplayCompleted<TResponse>(
        IdempotencyRecord record,
        IdempotentOperationRequest<TResponse> request,
        string idempotencyKeyHash)
    {
        if (record.ResponseStatusCode is null)
            throw new InvalidOperationException("Completed idempotency record has no response status code.");

        if (string.IsNullOrWhiteSpace(record.ResponseBody))
            throw new InvalidOperationException("Completed idempotency record has no response body.");

        var response = serializer.Deserialize<TResponse>(record.ResponseBody);

        LogReplayCompleted(logger, request.OperationName, idempotencyKeyHash);

        return new IdempotentOperationResult<TResponse>(
            IdempotentOperationResultKind.RecoveredFromPreviousExecution,
            response,
            record.ResponseStatusCode.Value,
            null);
    }

    private static IdempotentOperationResult<TResponse> InProgress<TResponse>(string message)
        => new(IdempotentOperationResultKind.InProgress, default, null, message);

    private async Task TrySaveFailureAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await records.SaveFailureAsync(record, cancellationToken);
        }
#pragma warning disable CA1031 // Failure persistence must not mask the original operation exception.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
        }
    }

    private static bool CanRetry(IdempotencyRecord record)
        => string.Equals(record.FailureStage, IdempotencyFailureStage.BeforeExternalSideEffect, StringComparison.Ordinal)
            || string.Equals(record.FailureStage, IdempotencyFailureStage.AfterIdentityProviderCompensated, StringComparison.Ordinal);

    private DateTime GetUtcNow()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static string HashKeyForLog(string idempotencyKey)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey)))[..12].ToLowerInvariant();

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Transicao de idempotencia. OperationName: {OperationName}; IdempotencyKeyHash: {IdempotencyKeyHash}; From: {FromStatus}; To: {ToStatus}")]
    private static partial void LogStatusTransition(
        ILogger logger,
        string operationName,
        string idempotencyKeyHash,
        string fromStatus,
        string toStatus);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Reserva concorrente de idempotencia tratada por unique constraint. OperationName: {OperationName}; IdempotencyKeyHash: {IdempotencyKeyHash}")]
    private static partial void LogConcurrentReservationDetected(
        ILogger logger,
        string operationName,
        string idempotencyKeyHash);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Conflito de payload idempotente. OperationName: {OperationName}; IdempotencyKeyHash: {IdempotencyKeyHash}")]
    private static partial void LogPayloadHashConflict(
        ILogger logger,
        string operationName,
        string idempotencyKeyHash);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Operacao idempotente ainda em andamento. OperationName: {OperationName}; IdempotencyKeyHash: {IdempotencyKeyHash}")]
    private static partial void LogOperationInProgress(
        ILogger logger,
        string operationName,
        string idempotencyKeyHash);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Resposta idempotente anterior retornada. OperationName: {OperationName}; IdempotencyKeyHash: {IdempotencyKeyHash}")]
    private static partial void LogReplayCompleted(
        ILogger logger,
        string operationName,
        string idempotencyKeyHash);
}
