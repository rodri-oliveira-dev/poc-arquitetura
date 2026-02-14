using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface ILedgerEntryRepository
{
    Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default);
}