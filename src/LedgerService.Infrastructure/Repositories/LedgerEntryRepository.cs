using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Infrastructure.Repositories;

public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly AppDbContext _context;

    public LedgerEntryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LedgerEntry?> GetCompensatingEntryAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default)
    {
        var externalReference = $"estorno:{lancamentoOriginalId:N}";
        return await _context.LedgerEntries
            .FirstOrDefaultAsync(x => x.ExternalReference == externalReference, cancellationToken);
    }

    public async Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddAsync(ledgerEntry, cancellationToken);
    }
}
