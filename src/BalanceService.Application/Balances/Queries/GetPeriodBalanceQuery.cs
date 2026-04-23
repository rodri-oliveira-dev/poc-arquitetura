using BalanceService.Application.Balances.Queries.Models;
using MediatR;

namespace BalanceService.Application.Balances.Queries;

/// <summary>
/// Query para buscar consolidado por período.
/// </summary>
public sealed record GetPeriodBalanceQuery(string MerchantId, DateOnly From, DateOnly To) : IRequest<PeriodBalanceReadModel>;
