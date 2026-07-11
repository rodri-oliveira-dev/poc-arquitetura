using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Idempotency;

using Microsoft.EntityFrameworkCore;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class ProcessedEventRepository : IProcessedEventRepository
{
    private readonly BalanceDbContext _context;

    public ProcessedEventRepository(BalanceDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        return await _context.ProcessedEvents
            .AsNoTracking()
            .AnyAsync(x => x.EventId == eventId, cancellationToken);
    }

    public async Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processedEvent);

        // Importante: usamos INSERT ... ON CONFLICT DO NOTHING para garantir idempotência
        // sem "quebrar" a transaction do PostgreSQL com unique violation (que exigiria rollback).
        // Retorna 1 quando inseriu, 0 quando já existia.
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO balance.processed_events (id, event_id, merchant_id, occurred_at, processed_at)
VALUES ({processedEvent.Id}, {processedEvent.EventId}, {processedEvent.MerchantId}, {processedEvent.OccurredAt}, {processedEvent.ProcessedAt})
ON CONFLICT (event_id) DO NOTHING;", cancellationToken);

        return rows == 1;
    }

    public async Task<int> DeleteByEventIdsAsync(
        IReadOnlyCollection<string> eventIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        if (eventIds.Count == 0)
            return 0;

        return await _context.ProcessedEvents
            .Where(x => eventIds.Contains(x.EventId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
