using BalanceService.Domain.Balances;

namespace BalanceService.Application.Abstractions.Persistence;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
        string merchantId,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken = default);

    Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default);
}
