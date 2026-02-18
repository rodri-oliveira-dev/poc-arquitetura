namespace BalanceService.Application.Balances.Queries;

/// <summary>
/// Query para buscar consolidado diário.
/// </summary>
public sealed record GetDailyBalanceQuery(string MerchantId, DateOnly Date);
