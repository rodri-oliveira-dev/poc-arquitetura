using System.Diagnostics;
using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries.Models;

using MediatR;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetDailyBalanceHandler : IRequestHandler<GetDailyBalanceQuery, DailyBalanceReadModel>
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Application");

    // LedgerEntryCreated.v1 nao carrega currency. BRL e a limitacao documentada da POC.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceReadRepository _readRepository;
    private readonly IClock _clock;

    public GetDailyBalanceHandler(
        IDailyBalanceReadRepository readRepository,
        IClock clock)
    {
        _readRepository = readRepository;
        _clock = clock;
    }

    public async Task<DailyBalanceReadModel> Handle(GetDailyBalanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("balance.query.daily", ActivityKind.Internal);
        activity?.SetTag("balance.merchant_id", request.MerchantId);
        activity?.SetTag("balance.date", request.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var found = await _readRepository.GetDailyAsync(request.MerchantId, request.Date, cancellationToken);
        if (found is not null)
            return found;

        // Padrao escolhido: retornar 200 com zeros quando nao houver dados.
        return new DailyBalanceReadModel(
            MerchantId: request.MerchantId,
            Date: request.Date,
            Currency: DefaultCurrency,
            TotalCredits: 0m,
            TotalDebits: 0m,
            NetBalance: 0m,
            AsOf: DateTimeOffset.MinValue,
            UpdatedAt: _clock.UtcNow);
    }
}
