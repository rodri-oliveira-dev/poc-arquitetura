namespace PaymentService.Domain.Payments;

public enum LedgerIntegrationStatus
{
    NotRequired = 0,
    Pending = 1,
    Processing = 2,
    RetryScheduled = 3,
    Completed = 4,
    FailedDefinitive = 5,
    DeadLetter = 6
}
