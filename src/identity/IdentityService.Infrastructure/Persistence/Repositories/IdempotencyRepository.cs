using IdentityService.Application.Idempotency;
using IdentityService.Application.Idempotency.Ports;

using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyRepository(IdentityDbContext context) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
        string operationName,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => context.IdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.OperationName == operationName && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
