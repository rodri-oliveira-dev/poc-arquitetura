using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Infrastructure.Repositories;

public sealed class EstornoLancamentoRepository : IEstornoLancamentoRepository
{
    private readonly AppDbContext _context;

    public EstornoLancamentoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EstornoLancamento?> GetByIdAsync(
        Guid estornoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == estornoId, cancellationToken);
    }

    public async Task<EstornoLancamento?> GetByIdForUpdateAsync(
        Guid estornoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(x => x.Id == estornoId, cancellationToken);
    }

    public async Task<IReadOnlyList<EstornoLancamento>> ListPendingAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .AsNoTracking()
            .Where(x => x.Status == EstornoLancamentoStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(maxItems)
            .ToListAsync(cancellationToken);
    }

    public async Task<EstornoLancamento?> GetActiveByLancamentoOriginalIdAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(
                x => x.LancamentoOriginalId == lancamentoOriginalId
                    && (x.Status == EstornoLancamentoStatus.Pending
                        || x.Status == EstornoLancamentoStatus.Processing),
                cancellationToken);
    }

    public async Task<EstornoLancamento?> GetCompletedByLancamentoOriginalIdAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(
                x => x.LancamentoOriginalId == lancamentoOriginalId
                    && x.Status == EstornoLancamentoStatus.Completed,
                cancellationToken);
    }

    public async Task AddAsync(EstornoLancamento estorno, CancellationToken cancellationToken = default)
    {
        await _context.EstornosLancamentos.AddAsync(estorno, cancellationToken);
    }
}
