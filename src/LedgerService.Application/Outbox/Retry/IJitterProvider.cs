namespace LedgerService.Application.Outbox.Retry;

public interface IJitterProvider
{
    TimeSpan NextJitter();
}
