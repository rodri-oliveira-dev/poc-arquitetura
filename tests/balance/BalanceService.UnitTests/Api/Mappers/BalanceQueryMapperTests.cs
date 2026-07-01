using BalanceService.Api.Mappers;

using FluentValidation;

namespace BalanceService.UnitTests.Api.Mappers;

public sealed class BalanceQueryMapperTests
{
    [Fact]
    public void ToDailyQuery_should_map_valid_request_values()
    {
        var result = BalanceQueryMapper.ToDailyQuery("m1", "2026-02-10");
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal(new DateOnly(2026, 2, 10), result.Date);
    }

    [Fact]
    public void ToDailyQuery_should_fail_when_date_is_invalid()
    {
        var act = () => BalanceQueryMapper.ToDailyQuery("m1", "10-02-2026");

        var ex = Assert.Throws<ValidationException>(act);
        Assert.Single(ex.Errors, e => e.PropertyName == "date");
    }

    [Fact]
    public void ToPeriodQuery_should_map_valid_request_values()
    {
        var result = BalanceQueryMapper.ToPeriodQuery("m1", "2026-02-10", "2026-02-12", maxPeriodDays: 31);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal(new DateOnly(2026, 2, 10), result.From);
        Assert.Equal(new DateOnly(2026, 2, 12), result.To);
    }

    [Fact]
    public void ToPeriodQuery_should_fail_when_to_is_invalid()
    {
        var act = () => BalanceQueryMapper.ToPeriodQuery("m1", "2026-02-10", "bad", maxPeriodDays: 31);

        var ex = Assert.Throws<ValidationException>(act);
        Assert.Single(ex.Errors, e => e.PropertyName == "to");
    }

    [Fact]
    public void ToPeriodQuery_should_fail_when_range_exceeds_max_period()
    {
        var act = () => BalanceQueryMapper.ToPeriodQuery("m1", "2026-02-10", "2026-02-12", maxPeriodDays: 2);

        var ex = Assert.Throws<ValidationException>(act);
        Assert.Single(ex.Errors, e => e.PropertyName == "to");
    }
}
