using LedgerService.Application.Idempotency;

using Microsoft.EntityFrameworkCore;

namespace LedgerService.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyRecordRepository(AppDbContext context) : IIdempotencyRecordRepository
{
    private readonly AppDbContext _context = context;

    public async Task<IdempotencyRecord?> GetByMerchantAndKeyAsync(string merchantId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId && x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken = default)
    {
        await _context.IdempotencyRecords.AddAsync(idempotencyRecord, cancellationToken);
    }
}
