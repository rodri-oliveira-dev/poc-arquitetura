using BalanceService.Application.Abstractions.Time;

namespace BalanceService.UnitTests.Application.Common.Time;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_should_be_close_to_system_utcnow()
    {
        var sut = new SystemClock();

        var expected = DateTimeOffset.UtcNow;
        var actual = sut.UtcNow;
        Assert.InRange(actual, expected - TimeSpan.FromSeconds(2), expected + TimeSpan.FromSeconds(2));
    }
}
