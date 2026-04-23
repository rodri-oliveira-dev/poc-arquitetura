using BalanceService.Application.Balances.Queries.Models;

namespace BalanceService.Application.Balances.Services;

/// <summary>
/// Serviço de leitura do consolidado por período.
/// </summary>
public interface IPeriodBalanceService
{
    Task<PeriodBalanceReadModel> GetPeriodAsync(string merchantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
