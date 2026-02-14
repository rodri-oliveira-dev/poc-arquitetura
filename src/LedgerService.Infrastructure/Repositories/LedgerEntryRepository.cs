using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;

namespace LedgerService.Infrastructure.Repositories;

public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly AppDbContext _context;

    public LedgerEntryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddAsync(ledgerEntry, cancellationToken);
    }
}