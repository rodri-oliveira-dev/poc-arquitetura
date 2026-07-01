using System.Diagnostics;
using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries.Models;

using MediatR;

namespace BalanceService.Application.Balances.Queries;

public sealed class GetPeriodBalanceHandler : IRequestHandler<GetPeriodBalanceQuery, PeriodBalanceReadModel>
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Application");

    // LedgerEntryCreated.v1 nao carrega currency. BRL e a limitacao documentada da POC.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceReadRepository _readRepository;
    private readonly IClock _clock;

    public GetPeriodBalanceHandler(
        IDailyBalanceReadRepository readRepository,
        IClock clock)
    {
        _readRepository = readRepository;
        _clock = clock;
    }

    public async Task<PeriodBalanceReadModel> Handle(GetPeriodBalanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("balance.query.period", ActivityKind.Internal);
        activity?.SetTag("balance.merchant_id", request.MerchantId);
        activity?.SetTag("balance.from", request.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        activity?.SetTag("balance.to", request.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var items = await _readRepository.ListByPeriodAsync(
            request.MerchantId,
            request.From,
            request.To,
            cancellationToken);

        var totalCredits = items.Sum(x => x.TotalCredits);
        var totalDebits = items.Sum(x => x.TotalDebits);

        // A tabela e (merchant,date,currency) e hoje escreve BRL default.
        var currency = items.Count > 0 ? items[0].Currency : DefaultCurrency;

        return new PeriodBalanceReadModel(
            MerchantId: request.MerchantId,
            From: request.From,
            To: request.To,
            Currency: currency,
            TotalCredits: totalCredits,
            TotalDebits: totalDebits,
            NetBalance: totalCredits - totalDebits,
            Items: items,
            CalculatedAt: _clock.UtcNow);
    }
}
