namespace PaymentService.Application.Payments.InboxProcessing;

public static class PaymentInboxRetryPolicy
{
    public static DateTimeOffset CalculateNextRetryAt(
        DateTimeOffset now,
        int attemptCount,
        TimeSpan baseDelay,
        TimeSpan maxDelay)
    {
        if (attemptCount <= 0)
            attemptCount = 1;

        var exponent = Math.Min(attemptCount - 1, 30);
        var multiplier = 1L << exponent;
        var delayTicks = baseDelay.Ticks > 0 && multiplier > long.MaxValue / baseDelay.Ticks
            ? long.MaxValue
            : baseDelay.Ticks * multiplier;
        var delay = TimeSpan.FromTicks(Math.Min(delayTicks, maxDelay.Ticks));

        return now.Add(delay);
    }
}
