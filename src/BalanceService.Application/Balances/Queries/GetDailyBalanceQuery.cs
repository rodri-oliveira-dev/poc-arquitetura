using BalanceService.Application.Balances.Queries.Models;
using MediatR;

namespace BalanceService.Application.Balances.Queries;

/// <summary>
/// Query para buscar consolidado diário.
/// </summary>
public sealed record GetDailyBalanceQuery(string MerchantId, DateOnly Date) : IRequest<DailyBalanceReadModel>;
