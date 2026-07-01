using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LedgerEntry?> GetCompensatingEntryAsync(Guid lancamentoOriginalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerEntry>> ListByMerchantAndPeriodAsync(
        string merchantId,
        DateTime startInclusive,
        DateTime endExclusive,
        CancellationToken cancellationToken = default);
    Task AddAsync(LedgerEntry ledgerEntry, CancellationToken cancellationToken = default);
}
