namespace PaymentService.Application.Payments.Ledger;

public static class PaymentLedgerRetryPolicy
{
    public static DateTimeOffset CalculateNextRetryAt(
        DateTimeOffset now,
        int attemptCount,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        TimeSpan? retryAfter = null)
    {
        if (retryAfter is { } providerDelay && providerDelay > TimeSpan.Zero)
            return now.Add(providerDelay <= maxDelay ? providerDelay : maxDelay);

        var exponent = Math.Max(0, attemptCount - 1);
        var multiplier = Math.Pow(2, Math.Min(exponent, 30));
        var delayTicks = Math.Min(baseDelay.Ticks * multiplier, maxDelay.Ticks);

        return now.Add(TimeSpan.FromTicks((long)delayTicks));
    }
}
