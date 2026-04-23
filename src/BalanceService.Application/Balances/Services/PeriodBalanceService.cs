using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries.Models;
using System.Diagnostics;

namespace BalanceService.Application.Balances.Services;

public sealed class PeriodBalanceService : IPeriodBalanceService
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Application");

    // TODO: confirmar origem/contrato de currency para consolidado. Hoje a consolidação escreve BRL como default.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceReadRepository _readRepository;
    private readonly IClock _clock;

    public PeriodBalanceService(
        IDailyBalanceReadRepository readRepository,
        IClock clock)
    {
        _readRepository = readRepository;
        _clock = clock;
    }

    public async Task<PeriodBalanceReadModel> GetPeriodAsync(string merchantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("balance.query.period", ActivityKind.Internal);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.from", from.ToString("yyyy-MM-dd"));
        activity?.SetTag("balance.to", to.ToString("yyyy-MM-dd"));

        var items = await _readRepository.ListByPeriodAsync(merchantId, from, to, cancellationToken);

        var totalCredits = items.Sum(x => x.TotalCredits);
        var totalDebits = items.Sum(x => x.TotalDebits);
        var net = totalCredits - totalDebits;

        // Currency: como a tabela é (merchant,date,currency) e hoje escreve BRL default,
        // para a POC assumimos a moeda do primeiro item, senão DefaultCurrency.
        var currency = items.FirstOrDefault()?.Currency ?? DefaultCurrency;

        return new PeriodBalanceReadModel(
            MerchantId: merchantId,
            From: from,
            To: to,
            Currency: currency,
            TotalCredits: totalCredits,
            TotalDebits: totalDebits,
            NetBalance: net,
            Items: items,
            CalculatedAt: _clock.UtcNow);
    }
}
