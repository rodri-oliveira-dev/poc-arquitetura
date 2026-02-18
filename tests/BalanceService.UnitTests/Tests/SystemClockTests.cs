using BalanceService.Application.Abstractions.Time;
using FluentAssertions;

namespace BalanceService.UnitTests.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_should_be_close_to_system_utcnow()
    {
        var sut = new SystemClock();

        var expected = DateTimeOffset.UtcNow;
        var actual = sut.UtcNow;

        actual.Should().BeCloseTo(expected, precision: TimeSpan.FromSeconds(2));
    }
}
