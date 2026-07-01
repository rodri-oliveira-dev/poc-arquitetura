namespace LedgerService.Application.Outbox.Retry;

public sealed class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    private readonly IJitterProvider _jitterProvider;

    public ExponentialBackoffRetryStrategy(IJitterProvider jitterProvider)
    {
        ArgumentNullException.ThrowIfNull(jitterProvider);

        _jitterProvider = jitterProvider;
    }

    public DateTime CalculateNextRetry(DateTime now, int retryCount, TimeSpan baseDelay)
    {
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be greater than zero.");

        var exponent = Math.Min(10, Math.Max(0, retryCount));
        var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, exponent));

        return now.Add(exponentialDelay).Add(_jitterProvider.NextJitter());
    }
}
