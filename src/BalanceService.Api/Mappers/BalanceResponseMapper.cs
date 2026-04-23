using System.Globalization;

using BalanceService.Api.Contracts;
using BalanceService.Application.Balances.Queries.Models;

namespace BalanceService.Api.Mappers;

public static class BalanceResponseMapper
{
    public static DailyBalanceResponse ToResponse(DailyBalanceReadModel model, DateTimeOffset calculatedAt)
        => new()
        {
            MerchantId = model.MerchantId,
            Date = model.Date.ToString("yyyy-MM-dd"),
            Currency = model.Currency,
            TotalCredits = model.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
            TotalDebits = model.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
            NetBalance = model.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
            AsOf = ToIsoOrNull(model.AsOf),
            CalculatedAt = calculatedAt.ToString("o")
        };

    public static PeriodBalanceResponse ToResponse(PeriodBalanceReadModel model, DateTimeOffset calculatedAt)
        => new()
        {
            MerchantId = model.MerchantId,
            From = model.From.ToString("yyyy-MM-dd"),
            To = model.To.ToString("yyyy-MM-dd"),
            Currency = model.Currency,
            TotalCredits = model.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
            TotalDebits = model.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
            NetBalance = model.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
            Items = model.Items
                .Select(ToItemResponse)
                .ToList(),
            CalculatedAt = calculatedAt.ToString("o")
        };

    private static PeriodBalanceItemResponse ToItemResponse(DailyBalanceReadModel model)
        => new()
        {
            Date = model.Date.ToString("yyyy-MM-dd"),
            TotalCredits = model.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
            TotalDebits = model.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
            NetBalance = model.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
            AsOf = ToIsoOrNull(model.AsOf)
        };

    private static string? ToIsoOrNull(DateTimeOffset value)
        => value == DateTimeOffset.MinValue ? null : value.ToString("o");
}
