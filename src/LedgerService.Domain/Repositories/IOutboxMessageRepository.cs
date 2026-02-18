using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

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

    Task MarkSentAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default);

    Task MarkFailedAttemptAsync(
        Guid id,
        int maxAttempts,
        DateTime nextAttemptAt,
        string? lastError,
        CancellationToken cancellationToken = default);
}