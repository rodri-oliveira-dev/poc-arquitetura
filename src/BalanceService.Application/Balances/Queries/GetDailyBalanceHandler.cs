using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using MediatR;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetDailyBalanceHandler : IRequestHandler<GetDailyBalanceQuery, DailyBalanceReadModel>
{
    private readonly IDailyBalanceService _dailyBalanceService;

    public GetDailyBalanceHandler(IDailyBalanceService dailyBalanceService)
    {
        _dailyBalanceService = dailyBalanceService;
    }

    public Task<DailyBalanceReadModel> Handle(GetDailyBalanceQuery request, CancellationToken cancellationToken)
        => _dailyBalanceService.GetDailyAsync(request.MerchantId, request.Date, cancellationToken);
}
