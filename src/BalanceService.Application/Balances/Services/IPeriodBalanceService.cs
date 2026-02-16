using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;

namespace BalanceService.Application.Balances.Services;

/// <summary>
/// Serviço de leitura do consolidado por período.
/// </summary>
public interface IPeriodBalanceService
{
    Task<PeriodBalanceReadModel> GetPeriodAsync(GetPeriodBalanceQuery query, CancellationToken cancellationToken);
}
