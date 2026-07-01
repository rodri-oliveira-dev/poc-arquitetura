namespace BalanceService.Application.Balances.Queries.Models;

/// <summary>
/// Resultado de leitura do consolidado por período.
/// </summary>
public sealed record PeriodBalanceReadModel(
    string MerchantId,
    DateOnly From,
    DateOnly To,
    string Currency,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal NetBalance,
    IReadOnlyList<DailyBalanceReadModel> Items,
    DateTimeOffset CalculatedAt);
