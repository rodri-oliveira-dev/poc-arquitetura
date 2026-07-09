namespace PaymentService.Application.Payments.InboxProcessing;

public sealed class PaymentInboxProcessingOptions
{
    public const string SectionName = "PaymentService:InboxWorker";

    public int MaxRetryCount { get; init; } = 5;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);
}
