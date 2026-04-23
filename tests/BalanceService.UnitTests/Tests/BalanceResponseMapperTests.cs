using BalanceService.Api.Mappers;
using BalanceService.Application.Balances.Queries.Models;

using FluentAssertions;

namespace BalanceService.UnitTests.Tests;

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

        result.MerchantId.Should().Be("m1");
        result.Date.Should().Be("2026-02-10");
        result.Currency.Should().Be("BRL");
        result.TotalCredits.Should().Be("150.50");
        result.TotalDebits.Should().Be("20.00");
        result.NetBalance.Should().Be("130.50");
        result.AsOf.Should().Be("2026-02-10T08:00:00.0000000+00:00");
        result.CalculatedAt.Should().Be("2026-02-16T12:30:00.0000000+00:00");
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

        result.MerchantId.Should().Be("m1");
        result.From.Should().Be("2026-02-10");
        result.To.Should().Be("2026-02-12");
        result.Currency.Should().Be("BRL");
        result.TotalCredits.Should().Be("150.00");
        result.TotalDebits.Should().Be("20.00");
        result.NetBalance.Should().Be("130.00");
        result.CalculatedAt.Should().Be("2026-02-16T12:30:00.0000000+00:00");
        result.Items.Should().ContainSingle();
        result.Items[0].Date.Should().Be("2026-02-10");
        result.Items[0].AsOf.Should().BeNull();
    }
}
