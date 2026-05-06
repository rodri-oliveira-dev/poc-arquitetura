using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface IEstornoLancamentoRepository
{
    Task<EstornoLancamento?> GetByIdAsync(
        Guid estornoId,
        CancellationToken cancellationToken = default);

    Task<EstornoLancamento?> GetActiveByLancamentoOriginalIdAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default);

    Task AddAsync(EstornoLancamento estorno, CancellationToken cancellationToken = default);
}
