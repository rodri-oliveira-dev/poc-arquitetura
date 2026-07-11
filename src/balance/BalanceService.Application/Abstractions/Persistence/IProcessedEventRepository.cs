using BalanceService.Application.Idempotency;

namespace BalanceService.Application.Abstractions.Persistence;

public interface IProcessedEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta inserir o registro de evento processado.
    /// Deve ser idempotente: se o EventId já existir, retorna false sem lançar erro.
    /// </summary>
    Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default);

    Task<int> DeleteByEventIdsAsync(
        IReadOnlyCollection<string> eventIds,
        CancellationToken cancellationToken = default);
}
