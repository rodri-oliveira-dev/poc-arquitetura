namespace PaymentService.Domain.Payments;

public enum PaymentStatus
{
    Pending = 1,
    RequiresAction = 2,
    Processing = 3,
    Succeeded = 4,
    LedgerPending = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8
}
