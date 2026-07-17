
namespace LedgerService.UnitTests.Fixtures;

public sealed class TestClock : TimeProvider
{
    public TestClock(DateTimeOffset? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow
    {
        get; set;
    }

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
