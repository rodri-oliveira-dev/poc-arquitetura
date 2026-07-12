namespace LedgerService.Application.Abstractions.Messaging;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz o claim (lock) de um lote de mensagens pendentes para processamento exclusivo.
    /// Deve ser seguro em concorrência (múltiplas instâncias do publisher).
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        DateTime now,
        string lockOwner,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default);

    Task<OutboxStatus> MarkFailedPublishAttemptAsync(
        Guid id,
        int maxRetries,
        DateTime nextRetryAt,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<OutboxMessage> Items, int TotalCount)> GetDeadLettersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> RequeueDeadLettersAsync(
        Guid? id,
        string? eventType,
        DateTime? occurredFrom,
        DateTime? occurredUntil,
        int limit,
        DateTime requeuedAt,
        string requeuedBy,
        string reason,
        CancellationToken cancellationToken = default);
}
