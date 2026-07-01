namespace BalanceService.Application.Balances.Queries.Models;

/// <summary>
/// Modelo de leitura do consolidado diário (projeção da tabela <c>daily_balances</c>).
/// </summary>
public sealed record DailyBalanceReadModel(
    string MerchantId,
    DateOnly Date,
    string Currency,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal NetBalance,
    DateTimeOffset AsOf,
    DateTimeOffset UpdatedAt);
