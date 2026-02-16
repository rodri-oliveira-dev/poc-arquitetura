using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly BalanceDbContext _context;

    public DailyBalanceRepository(BalanceDbContext context)
    {
        _context = context;
    }

    public async Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        return await _context.DailyBalances
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId && x.Date == date && x.Currency == normalizedCurrency,
                cancellationToken);
    }

    public Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
        => _context.DailyBalances.AddAsync(dailyBalance, cancellationToken).AsTask();
}
