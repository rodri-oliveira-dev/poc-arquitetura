using LedgerService.Application.Abstractions.Time;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LedgerService.Infrastructure.Persistence.Repositories;

public sealed class EstornoLancamentoRepository : IEstornoLancamentoRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public EstornoLancamentoRepository(AppDbContext context, IClock? clock = null)
    {
        _context = context;
        _clock = clock ?? new SystemClock();
    }

    public async Task<EstornoLancamento?> GetByIdAsync(
        Guid estornoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == estornoId, cancellationToken);
    }

    public async Task<EstornoLancamento?> GetByIdForUpdateAsync(
        Guid estornoId,
        CancellationToken cancellationToken = default)
    {
        if (IsNpgsql())
        {
            return await _context.EstornosLancamentos
                .FromSqlInterpolated(
                    $"SELECT * FROM estornos_lancamentos WHERE id = {estornoId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(x => x.Id == estornoId, cancellationToken);
    }

    public async Task<IReadOnlyList<EstornoLancamento>> ClaimPendingAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (IsNpgsql())
        {
            var sql = @"
WITH cte AS (
    SELECT id
    FROM estornos_lancamentos
    WHERE status = @p_pending
    ORDER BY created_at
    FOR UPDATE SKIP LOCKED
    LIMIT @p_batch
)
UPDATE estornos_lancamentos e
SET status = @p_processing,
    processing_started_at = COALESCE(e.processing_started_at, @p_now),
    failure_reason = NULL,
    rejection_reason = NULL
FROM cte
WHERE e.id = cte.id
RETURNING e.*;
";

            return await _context.EstornosLancamentos.FromSqlRaw(
                sql,
                new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = EstornoLancamentoStatus.Pending.ToString() },
                new NpgsqlParameter("p_processing", NpgsqlDbType.Text) { Value = EstornoLancamentoStatus.Processing.ToString() },
                new NpgsqlParameter("p_now", NpgsqlDbType.Timestamp) { Value = _clock.UtcNow.DateTime },
                new NpgsqlParameter("p_batch", NpgsqlDbType.Integer) { Value = maxItems })
                .ToListAsync(cancellationToken);
        }

        var now = _clock.UtcNow.DateTime;
        var candidates = await _context.EstornosLancamentos
            .Where(x => x.Status == EstornoLancamentoStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(maxItems)
            .ToListAsync(cancellationToken);

        foreach (var estorno in candidates)
        {
            estorno.MarkProcessing(now);
        }

        return candidates;
    }

    public async Task<EstornoLancamento?> GetActiveByLancamentoOriginalIdAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(
                x => x.LancamentoOriginalId == lancamentoOriginalId
                    && (x.Status == EstornoLancamentoStatus.Pending
                        || x.Status == EstornoLancamentoStatus.Processing),
                cancellationToken);
    }

    public async Task<EstornoLancamento?> GetCompletedByLancamentoOriginalIdAsync(
        Guid lancamentoOriginalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstornosLancamentos
            .FirstOrDefaultAsync(
                x => x.LancamentoOriginalId == lancamentoOriginalId
                    && x.Status == EstornoLancamentoStatus.Completed,
                cancellationToken);
    }

    public async Task AddAsync(EstornoLancamento estorno, CancellationToken cancellationToken = default)
    {
        await _context.EstornosLancamentos.AddAsync(estorno, cancellationToken);
    }

    private bool IsNpgsql()
        => string.Equals(
            _context.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);
}
