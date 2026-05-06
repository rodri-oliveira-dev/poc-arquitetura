using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default);
}
