using System.Security.Cryptography;
using System.Text;

using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentInboxMessage
{
    public const int ProviderEventIdMaxLength = 200;
    public const int EventTypeMaxLength = 200;
    public const int HashMaxLength = 64;
    public const int CorrelationIdMaxLength = 100;
    public const int ProviderPaymentIdMaxLength = 200;
    public const int LastErrorMaxLength = 1000;
    public const int LockOwnerMaxLength = 200;

    private PaymentInboxMessage()
    {
    }

    public Guid Id
    {
        get; private set;
    }

    public PaymentProvider Provider
    {
        get; private set;
    }

    public string ProviderEventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public string PayloadSha256 { get; private set; } = string.Empty;

    public PaymentInboxStatus Status
    {
        get; private set;
    }

    public StripeWebhookEventCategory EventCategory
    {
        get; private set;
    }

    public DateTimeOffset ReceivedAt
    {
        get; private set;
    }

    public DateTimeOffset? ProcessedAt
    {
        get; private set;
    }

    public int AttemptCount
    {
        get; private set;
    }

    public DateTimeOffset? NextRetryAt
    {
        get; private set;
    }

    public string? LastError
    {
        get; private set;
    }

    public DateTimeOffset? ProcessingStartedAt
    {
        get; private set;
    }

    public string? LockOwner
    {
        get; private set;
    }

    public DateTimeOffset? LockedUntil
    {
        get; private set;
    }

    public string? CorrelationId
    {
        get; private set;
    }

    public string? ProviderPaymentId
    {
        get; private set;
    }

    public PaymentId? PaymentId
    {
        get; private set;
    }

    public DateTimeOffset CreatedAt
    {
        get; private set;
    }

    public DateTimeOffset UpdatedAt
    {
        get; private set;
    }

    public static PaymentInboxMessage CreateStripe(
        string providerEventId,
        string eventType,
        string payload,
        DateTimeOffset receivedAt,
        string? correlationId,
        string? providerPaymentId,
        PaymentId? paymentId)
    {
        var category = StripeWebhookEventClassifier.Classify(eventType);
        var status = category == StripeWebhookEventCategory.Supported
            ? PaymentInboxStatus.Pending
            : PaymentInboxStatus.Ignored;

        return new PaymentInboxMessage
        {
            Id = Guid.NewGuid(),
            Provider = PaymentProvider.Stripe,
            ProviderEventId = NormalizeRequired(providerEventId, ProviderEventIdMaxLength, nameof(providerEventId)),
            EventType = NormalizeRequired(eventType, EventTypeMaxLength, nameof(eventType)),
            Payload = string.IsNullOrWhiteSpace(payload)
                ? throw new ArgumentException("Payload da Inbox nao pode ser vazio.", nameof(payload))
                : payload,
            PayloadSha256 = NormalizeRequired(ComputeSha256(payload), HashMaxLength, nameof(payload)),
            Status = status,
            EventCategory = category,
            ReceivedAt = receivedAt,
            CreatedAt = receivedAt,
            UpdatedAt = receivedAt,
            CorrelationId = NormalizeOptional(correlationId, CorrelationIdMaxLength, nameof(correlationId)),
            ProviderPaymentId = NormalizeOptional(providerPaymentId, ProviderPaymentIdMaxLength, nameof(providerPaymentId)),
            PaymentId = paymentId
        };
    }

    public void MarkProcessing(string lockOwner, DateTimeOffset now, DateTimeOffset lockedUntil)
    {
        LockOwner = NormalizeRequired(lockOwner, LockOwnerMaxLength, nameof(lockOwner));
        ProcessingStartedAt = now;
        LockedUntil = lockedUntil;
        Status = PaymentInboxStatus.Processing;
        AttemptCount++;
        LastError = null;
        NextRetryAt = null;
        UpdatedAt = now;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        Status = PaymentInboxStatus.Processed;
        ProcessedAt = processedAt;
        ClearProcessingState();
        LastError = null;
        NextRetryAt = null;
        UpdatedAt = processedAt;
    }

    public void MarkIgnored(DateTimeOffset processedAt, string reason)
    {
        Status = PaymentInboxStatus.Ignored;
        ProcessedAt = processedAt;
        ClearProcessingState();
        LastError = NormalizeOptional(reason, LastErrorMaxLength, nameof(reason));
        NextRetryAt = null;
        UpdatedAt = processedAt;
    }

    public void ScheduleRetry(DateTimeOffset now, DateTimeOffset nextRetryAt, string lastError)
    {
        Status = PaymentInboxStatus.RetryScheduled;
        ProcessedAt = null;
        ClearProcessingState();
        LastError = NormalizeOptional(lastError, LastErrorMaxLength, nameof(lastError));
        NextRetryAt = nextRetryAt;
        UpdatedAt = now;
    }

    public void MarkDeadLetter(DateTimeOffset deadLetteredAt, string lastError)
    {
        Status = PaymentInboxStatus.DeadLetter;
        ProcessedAt = deadLetteredAt;
        ClearProcessingState();
        LastError = NormalizeOptional(lastError, LastErrorMaxLength, nameof(lastError));
        NextRetryAt = null;
        UpdatedAt = deadLetteredAt;
    }

    private void ClearProcessingState()
    {
        ProcessingStartedAt = null;
        LockOwner = null;
        LockedUntil = null;
    }

    private static string ComputeSha256(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} nao pode ser vazio.", fieldName);

        var normalized = value.Trim();
        return normalized.Length > maxLength
            ? throw new ArgumentException($"{fieldName} deve ter no maximo {maxLength} caracteres.", fieldName)
            : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length > maxLength
            ? throw new ArgumentException($"{fieldName} deve ter no maximo {maxLength} caracteres.", fieldName)
            : normalized;
    }
}
