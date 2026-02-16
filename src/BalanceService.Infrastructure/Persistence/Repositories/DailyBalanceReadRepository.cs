using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Queries.Models;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class DailyBalanceReadRepository : IDailyBalanceReadRepository
{
    private readonly BalanceDbContext _context;

    public DailyBalanceReadRepository(BalanceDbContext context)
    {
        _context = context;
    }

    public async Task<DailyBalanceReadModel?> GetDailyAsync(
        string merchantId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyBalances
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId && x.Date == date)
            .OrderBy(x => x.Currency)
            .Select(x => new DailyBalanceReadModel(
                x.MerchantId,
                x.Date,
                x.Currency,
                x.TotalCredits,
                x.TotalDebits,
                x.NetBalance,
                x.AsOf,
                x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyBalanceReadModel>> ListByPeriodAsync(
        string merchantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Observação: a tabela é (merchant,date,currency). A API não recebe currency.
        // Para a POC (onde hoje escreve BRL default), retornamos todos os registros.
        // TODO: quando existir múltipla moeda, decidir política (filtrar por currency, ou separar por moeda).
        return await _context.DailyBalances
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Currency)
            .Select(x => new DailyBalanceReadModel(
                x.MerchantId,
                x.Date,
                x.Currency,
                x.TotalCredits,
                x.TotalDebits,
                x.NetBalance,
                x.AsOf,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
