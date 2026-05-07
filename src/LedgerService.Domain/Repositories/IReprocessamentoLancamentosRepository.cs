using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface IReprocessamentoLancamentosRepository
{
    Task<ReprocessamentoLancamentos?> GetByIdAsync(
        Guid reprocessamentoId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ReprocessamentoLancamentos reprocessamento,
        CancellationToken cancellationToken = default);
}
