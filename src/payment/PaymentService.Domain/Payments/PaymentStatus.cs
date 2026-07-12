namespace PaymentService.Domain.Payments;

public enum PaymentStatus
{
    Pending = 1,
    RequiresAction = 2,
    Processing = 3,
    Succeeded = 4,
    LedgerPending = 5,
    Completed = 6,
    PartiallyRefunded = 7,
    Refunded = 8,
    Failed = 9,
    Cancelled = 10
}
