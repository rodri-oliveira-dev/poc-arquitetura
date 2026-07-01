using LedgerService.Application.Outbox.Retry;

namespace LedgerService.Worker.Tests.Outbox;

public sealed class OutboxRetryStrategyTests
{
    [Fact]
    public void CalculateNextRetry_should_use_exponential_backoff_with_bounded_jitter()
    {
        var now = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var sut = new ExponentialBackoffRetryStrategy(new FixedJitterProvider(TimeSpan.FromMilliseconds(100)));

        var nextAttempt = sut.CalculateNextRetry(now, retryCount: 2, TimeSpan.FromSeconds(2));

        Assert.True(nextAttempt >= now.AddSeconds(8));
        Assert.True(nextAttempt < now.AddSeconds(8).AddMilliseconds(250));
    }

    private sealed class FixedJitterProvider : IJitterProvider
    {
        private readonly TimeSpan _jitter;

        public FixedJitterProvider(TimeSpan jitter) => _jitter = jitter;

        public TimeSpan NextJitter() => _jitter;
    }
}
