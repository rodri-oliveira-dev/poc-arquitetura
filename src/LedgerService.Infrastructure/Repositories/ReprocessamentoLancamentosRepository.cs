using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Infrastructure.Repositories;

public sealed class ReprocessamentoLancamentosRepository : IReprocessamentoLancamentosRepository
{
    private readonly AppDbContext _context;

    public ReprocessamentoLancamentosRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReprocessamentoLancamentos?> GetByIdAsync(
        Guid reprocessamentoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ReprocessamentosLancamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reprocessamentoId, cancellationToken);
    }

    public async Task AddAsync(
        ReprocessamentoLancamentos reprocessamento,
        CancellationToken cancellationToken = default)
    {
        await _context.ReprocessamentosLancamentos.AddAsync(reprocessamento, cancellationToken);
    }
}
