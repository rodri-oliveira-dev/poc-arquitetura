namespace PaymentService.Application.Payments.InboxProcessing;

public enum PaymentProviderEventKind
{
    Processing,
    Succeeded,
    Failed,
    Cancelled
}
