using BalanceService.Api.Mappers;
using BalanceService.Application.Balances.Queries.Models;


namespace BalanceService.UnitTests.Api.Mappers;

public sealed class BalanceResponseMapperTests
{
    [Fact]
    public void ToResponse_should_map_daily_balance_contract()
    {
        var calculatedAt = new DateTimeOffset(2026, 2, 16, 12, 30, 0, TimeSpan.Zero);
        var model = new DailyBalanceReadModel(
            "m1",
            new DateOnly(2026, 2, 10),
            "BRL",
            150.5m,
            20m,
            130.5m,
            new DateTimeOffset(2026, 2, 10, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero));

        var result = BalanceResponseMapper.ToResponse(model, calculatedAt);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal("2026-02-10", result.Date);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal("150.50", result.TotalCredits);
        Assert.Equal("20.00", result.TotalDebits);
        Assert.Equal("130.50", result.NetBalance);
        Assert.Equal("2026-02-10T08:00:00.0000000+00:00", result.AsOf);
        Assert.Equal("2026-02-16T12:30:00.0000000+00:00", result.CalculatedAt);
    }

    [Fact]
    public void ToResponse_should_map_period_balance_contract_and_null_minvalue_asof()
    {
        var calculatedAt = new DateTimeOffset(2026, 2, 16, 12, 30, 0, TimeSpan.Zero);
        var model = new PeriodBalanceReadModel(
            "m1",
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 2, 12),
            "BRL",
            150m,
            20m,
            130m,
            new[]
            {
                new DailyBalanceReadModel(
                    "m1",
                    new DateOnly(2026, 2, 10),
                    "BRL",
                    150m,
                    20m,
                    130m,
                    DateTimeOffset.MinValue,
                    new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero))
            },
            new DateTimeOffset(2026, 2, 16, 11, 0, 0, TimeSpan.Zero));

        var result = BalanceResponseMapper.ToResponse(model, calculatedAt);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal("2026-02-10", result.From);
        Assert.Equal("2026-02-12", result.To);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal("150.00", result.TotalCredits);
        Assert.Equal("20.00", result.TotalDebits);
        Assert.Equal("130.00", result.NetBalance);
        Assert.Equal("2026-02-16T12:30:00.0000000+00:00", result.CalculatedAt);
        Assert.Single(result.Items);
        Assert.Equal("2026-02-10", result.Items[0].Date);
        Assert.Null(result.Items[0].AsOf);
    }
}
