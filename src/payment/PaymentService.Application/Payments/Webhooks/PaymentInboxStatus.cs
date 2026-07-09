namespace PaymentService.Application.Payments.Webhooks;

public enum PaymentInboxStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Ignored = 4,
    RetryScheduled = 5,
    DeadLetter = 6
}
