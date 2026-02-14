using Microsoft.EntityFrameworkCore;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;

namespace LedgerService.Infrastructure.Repositories;

public sealed class IdempotencyRecordRepository : IIdempotencyRecordRepository
{
    private readonly AppDbContext _context;

    public IdempotencyRecordRepository(AppDbContext context)
    {
        _context = context;
    }

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