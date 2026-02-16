using BalanceService.Domain.Balances;

namespace BalanceService.Application.Abstractions.Persistence;

public interface IProcessedEventRepository
{
    /// <summary>
    /// Tenta inserir o registro de evento processado.
    /// Deve ser idempotente: se o EventId já existir, retorna false sem lançar erro.
    /// </summary>
    Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default);
}
