namespace LedgerService.Application.Outbox.Retry;

public interface IRetryStrategy
{
    DateTime CalculateNextRetry(DateTime now, int retryCount, TimeSpan baseDelay);
}
