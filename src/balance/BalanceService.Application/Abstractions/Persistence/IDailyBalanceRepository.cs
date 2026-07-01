using BalanceService.Domain.Balances;

namespace BalanceService.Application.Abstractions.Persistence;

public interface IDailyBalanceRepository
{
    Task LockByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default);

    Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default);

    Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default);

    Task<int> DeleteByMerchantAndDateRangeAsync(
        string merchantId,
        DateOnly from,
        DateOnly until,
        CancellationToken cancellationToken = default);
}
