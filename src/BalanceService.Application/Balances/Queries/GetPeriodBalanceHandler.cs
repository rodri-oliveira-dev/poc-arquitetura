using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using MediatR;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetPeriodBalanceHandler : IRequestHandler<GetPeriodBalanceQuery, PeriodBalanceReadModel>
{
    private readonly IPeriodBalanceService _periodBalanceService;

    public GetPeriodBalanceHandler(IPeriodBalanceService periodBalanceService)
    {
        _periodBalanceService = periodBalanceService;
    }

    public Task<PeriodBalanceReadModel> Handle(GetPeriodBalanceQuery request, CancellationToken cancellationToken)
        => _periodBalanceService.GetPeriodAsync(request.MerchantId, request.From, request.To, cancellationToken);
}
