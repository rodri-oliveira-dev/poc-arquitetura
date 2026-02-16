using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BalanceService.Application.Balances.Services;

public sealed class PeriodBalanceService : IPeriodBalanceService
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Application");

    // TODO: confirmar origem/contrato de currency para consolidado. Hoje a consolidação escreve BRL como default.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceReadRepository _readRepository;
    private readonly IClock _clock;
    private readonly ILogger<PeriodBalanceService> _logger;

    public PeriodBalanceService(
        IDailyBalanceReadRepository readRepository,
        IClock clock,
        ILogger<PeriodBalanceService> logger)
    {
        _readRepository = readRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PeriodBalanceReadModel> GetPeriodAsync(GetPeriodBalanceQuery query, CancellationToken cancellationToken)
    {
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MerchantId"] = query.MerchantId,
            ["From"] = query.From.ToString("yyyy-MM-dd"),
            ["To"] = query.To.ToString("yyyy-MM-dd")
        });

        using var activity = ActivitySource.StartActivity("balance.query.period", ActivityKind.Internal);
        activity?.SetTag("balance.merchant_id", query.MerchantId);
        activity?.SetTag("balance.from", query.From.ToString("yyyy-MM-dd"));
        activity?.SetTag("balance.to", query.To.ToString("yyyy-MM-dd"));

        var items = await _readRepository.ListByPeriodAsync(query.MerchantId, query.From, query.To, cancellationToken);

        var totalCredits = items.Sum(x => x.TotalCredits);
        var totalDebits = items.Sum(x => x.TotalDebits);
        var net = totalCredits - totalDebits;

        // Currency: como a tabela é (merchant,date,currency) e hoje escreve BRL default,
        // para a POC assumimos a moeda do primeiro item, senão DefaultCurrency.
        var currency = items.FirstOrDefault()?.Currency ?? DefaultCurrency;

        return new PeriodBalanceReadModel(
            MerchantId: query.MerchantId,
            From: query.From,
            To: query.To,
            Currency: currency,
            TotalCredits: totalCredits,
            TotalDebits: totalDebits,
            NetBalance: net,
            Items: items,
            CalculatedAt: _clock.UtcNow);
    }
}
