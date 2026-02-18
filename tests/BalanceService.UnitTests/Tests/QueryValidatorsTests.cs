using BalanceService.Application.Balances.Queries;
using FluentAssertions;

namespace BalanceService.UnitTests.Tests;

public sealed class QueryValidatorsTests
{
    [Fact]
    public void GetDailyBalanceQueryValidator_should_fail_when_merchantid_empty()
    {
        var sut = new GetDailyBalanceQueryValidator();
        var result = sut.Validate(new GetDailyBalanceQuery("", new DateOnly(2026, 2, 10)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetDailyBalanceQueryValidator_should_pass_when_merchantid_present()
    {
        var sut = new GetDailyBalanceQueryValidator();
        var result = sut.Validate(new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetPeriodBalanceQueryValidator_should_fail_when_from_greater_than_to()
    {
        var sut = new GetPeriodBalanceQueryValidator();
        var result = sut.Validate(new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 11), new DateOnly(2026, 2, 10)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("From must be", StringComparison.Ordinal));
    }

    [Fact]
    public void GetPeriodBalanceQueryValidator_should_pass_when_from_less_or_equal_to_to()
    {
        var sut = new GetPeriodBalanceQueryValidator();
        var result = sut.Validate(new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 10)));
        result.IsValid.Should().BeTrue();
    }
}
