namespace PaymentService.Domain.Payments;

public enum RefundStatus
{
    Requested = 1,
    ProviderPending = 2,
    ProviderSucceeded = 3,
    ProviderFailed = 4,
    LedgerReversalPending = 5,
    LedgerReversalCompleted = 6,
    Completed = 7,
    Failed = 8,
    DeadLetter = 9
}
