using BalanceService.Application.Balances.Queries.Models;

namespace BalanceService.Application.Abstractions.Persistence;

/// <summary>
/// Repositório de leitura (queries) do consolidado diário.
/// Fonte de dados: tabela <c>daily_balances</c>.
/// </summary>
public interface IDailyBalanceReadRepository
{
    /// <summary>
    /// Obtém o consolidado de um dia para um merchant.
    /// </summary>
    Task<DailyBalanceReadModel?> GetDailyAsync(
        string merchantId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os consolidados diários no intervalo (inclusive).
    /// </summary>
    Task<IReadOnlyList<DailyBalanceReadModel>> ListByPeriodAsync(
        string merchantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
