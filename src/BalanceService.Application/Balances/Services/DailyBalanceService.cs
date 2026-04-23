using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BalanceService.Application.Balances.Services;

public sealed class DailyBalanceService : IDailyBalanceService
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Application");

    // TODO: confirmar origem/contrato de currency para consolidado. Hoje a consolidação escreve BRL como default.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceReadRepository _readRepository;
    private readonly IClock _clock;
    private readonly ILogger<DailyBalanceService> _logger;

    public DailyBalanceService(
        IDailyBalanceReadRepository readRepository,
        IClock clock,
        ILogger<DailyBalanceService> logger)
    {
        _readRepository = readRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DailyBalanceReadModel> GetDailyAsync(string merchantId, DateOnly date, CancellationToken cancellationToken)
    {
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MerchantId"] = merchantId,
            ["Date"] = date.ToString("yyyy-MM-dd")
        });

        using var activity = ActivitySource.StartActivity("balance.query.daily", ActivityKind.Internal);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.date", date.ToString("yyyy-MM-dd"));

        var found = await _readRepository.GetDailyAsync(merchantId, date, cancellationToken);
        if (found is not null)
            return found;

        // Padrão escolhido: retornar 200 com zeros quando não houver dados.
        // Isso melhora a UX para consultas.
        return new DailyBalanceReadModel(
            MerchantId: merchantId,
            Date: date,
            Currency: DefaultCurrency,
            TotalCredits: 0m,
            TotalDebits: 0m,
            NetBalance: 0m,
            AsOf: DateTimeOffset.MinValue,
            UpdatedAt: _clock.UtcNow);
    }
}
