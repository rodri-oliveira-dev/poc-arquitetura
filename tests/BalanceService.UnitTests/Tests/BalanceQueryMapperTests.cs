using BalanceService.Api.Mappers;

using FluentAssertions;
using FluentValidation;

namespace BalanceService.UnitTests.Tests;

public sealed class BalanceQueryMapperTests
{
    [Fact]
    public void ToDailyQuery_should_map_valid_request_values()
    {
        var result = BalanceQueryMapper.ToDailyQuery("m1", "2026-02-10");

        result.MerchantId.Should().Be("m1");
        result.Date.Should().Be(new DateOnly(2026, 2, 10));
    }

    [Fact]
    public void ToDailyQuery_should_fail_when_date_is_invalid()
    {
        var act = () => BalanceQueryMapper.ToDailyQuery("m1", "10-02-2026");

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "date");
    }

    [Fact]
    public void ToPeriodQuery_should_map_valid_request_values()
    {
        var result = BalanceQueryMapper.ToPeriodQuery("m1", "2026-02-10", "2026-02-12");

        result.MerchantId.Should().Be("m1");
        result.From.Should().Be(new DateOnly(2026, 2, 10));
        result.To.Should().Be(new DateOnly(2026, 2, 12));
    }

    [Fact]
    public void ToPeriodQuery_should_fail_when_to_is_invalid()
    {
        var act = () => BalanceQueryMapper.ToPeriodQuery("m1", "2026-02-10", "bad");

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "to");
    }
}
