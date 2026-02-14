using LedgerService.Domain.Entities;

namespace LedgerService.Domain.Repositories;

public interface IIdempotencyRecordRepository
{
    Task<IdempotencyRecord?> GetByMerchantAndKeyAsync(string merchantId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken = default);
}