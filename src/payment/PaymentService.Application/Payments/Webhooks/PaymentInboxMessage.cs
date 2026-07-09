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

    private PaymentInboxMessage(
        Guid id,
        PaymentProvider provider,
        string providerEventId,
        string eventType,
        string payload,
        string payloadSha256,
        PaymentInboxStatus status,
        StripeWebhookEventCategory eventCategory,
        DateTimeOffset receivedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? correlationId,
        string? providerPaymentId,
        PaymentId? paymentId)
    {
        Id = id;
        Provider = provider;
        ProviderEventId = NormalizeRequired(providerEventId, ProviderEventIdMaxLength, nameof(providerEventId));
        EventType = NormalizeRequired(eventType, EventTypeMaxLength, nameof(eventType));
        Payload = string.IsNullOrWhiteSpace(payload)
            ? throw new ArgumentException("Payload da Inbox nao pode ser vazio.", nameof(payload))
            : payload;
        PayloadSha256 = NormalizeRequired(payloadSha256, HashMaxLength, nameof(payloadSha256));
        Status = status;
        EventCategory = eventCategory;
        ReceivedAt = receivedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CorrelationId = NormalizeOptional(correlationId, CorrelationIdMaxLength, nameof(correlationId));
        ProviderPaymentId = NormalizeOptional(providerPaymentId, ProviderPaymentIdMaxLength, nameof(providerPaymentId));
        PaymentId = paymentId;
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

        return new PaymentInboxMessage(
            Guid.NewGuid(),
            PaymentProvider.Stripe,
            providerEventId,
            eventType,
            payload,
            ComputeSha256(payload),
            status,
            category,
            receivedAt,
            receivedAt,
            receivedAt,
            correlationId,
            providerPaymentId,
            paymentId);
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
