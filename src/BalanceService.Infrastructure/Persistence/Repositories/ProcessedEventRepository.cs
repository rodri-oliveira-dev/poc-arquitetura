using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Domain.Balances;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class ProcessedEventRepository : IProcessedEventRepository
{
    private readonly BalanceDbContext _context;

    public ProcessedEventRepository(BalanceDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
    {
        // Importante: usamos INSERT ... ON CONFLICT DO NOTHING para garantir idempotência
        // sem "quebrar" a transaction do PostgreSQL com unique violation (que exigiria rollback).
        // Retorna 1 quando inseriu, 0 quando já existia.
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO processed_events (id, event_id, merchant_id, occurred_at, processed_at)
VALUES ({processedEvent.Id}, {processedEvent.EventId}, {processedEvent.MerchantId}, {processedEvent.OccurredAt}, {processedEvent.ProcessedAt})
ON CONFLICT (event_id) DO NOTHING;", cancellationToken);

        return rows == 1;
    }
}
