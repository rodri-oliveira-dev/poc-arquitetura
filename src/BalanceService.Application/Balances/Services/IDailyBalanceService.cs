using BalanceService.Application.Balances.Queries.Models;

namespace BalanceService.Application.Balances.Services;

/// <summary>
/// Serviço de leitura do consolidado diário.
/// </summary>
public interface IDailyBalanceService
{
    Task<DailyBalanceReadModel> GetDailyAsync(string merchantId, DateOnly date, CancellationToken cancellationToken);
}
