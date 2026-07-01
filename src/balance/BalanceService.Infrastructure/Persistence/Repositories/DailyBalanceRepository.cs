using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using System.Globalization;

namespace BalanceService.Infrastructure.Persistence.Repositories;

public sealed class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly BalanceDbContext _context;

    public DailyBalanceRepository(BalanceDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task LockByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(merchantId);
        ArgumentNullException.ThrowIfNull(currency);

        if (!IsNpgsql())
            return;

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var lockKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{merchantId}|{date:yyyy-MM-dd}|{normalizedCurrency}");

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));",
            cancellationToken);
    }

    public async Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(merchantId);
        ArgumentNullException.ThrowIfNull(currency);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        return await _context.DailyBalances
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId && x.Date == date && x.Currency == normalizedCurrency,
                cancellationToken);
    }

    public Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dailyBalance);

        return _context.DailyBalances.AddAsync(dailyBalance, cancellationToken).AsTask();
    }

    public async Task<int> DeleteByMerchantAndDateRangeAsync(
        string merchantId,
        DateOnly from,
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(merchantId);

        return await _context.DailyBalances
            .Where(x => x.MerchantId == merchantId && x.Date >= from && x.Date <= until)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private bool IsNpgsql()
        => string.Equals(
            _context.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);
}
