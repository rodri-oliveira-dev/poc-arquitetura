using System.Diagnostics;
using System.Diagnostics.Metrics;

using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Api.Webhooks;

public sealed class PaymentWebhookTelemetry : IDisposable
{
    public const string MeterName = "PaymentService.Webhooks";
    private const string ActivitySourceName = "PaymentService.Api";
    private const string ProviderTagName = "provider";
    private const string ProviderTagValue = "Stripe";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter;
    private readonly Counter<long> _receivedCounter;
    private readonly Counter<long> _invalidSignatureCounter;
    private readonly Counter<long> _duplicateCounter;
    private readonly Counter<long> _persistFailureCounter;
    private readonly Counter<long> _ignoredCounter;
    private readonly UpDownCounter<long> _pendingCounter;

    public PaymentWebhookTelemetry(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName);
        _receivedCounter = _meter.CreateCounter<long>("payment_webhook_received_total");
        _invalidSignatureCounter = _meter.CreateCounter<long>("payment_webhook_invalid_signature_total");
        _duplicateCounter = _meter.CreateCounter<long>("payment_webhook_duplicate_total");
        _persistFailureCounter = _meter.CreateCounter<long>("payment_webhook_persist_failure_total");
        _ignoredCounter = _meter.CreateCounter<long>("payment_webhook_ignored_total");
        _pendingCounter = _meter.CreateUpDownCounter<long>("payment_inbox_pending_total");
    }

    public Activity? StartSignatureValidation()
        => _activitySource.StartActivity("stripe webhook signature validation", ActivityKind.Internal);

    public Activity? StartInboxPersist()
        => _activitySource.StartActivity("payment inbox persist", ActivityKind.Internal);

    public void RecordReceived(string outcome, StripeWebhookEventCategory? category = null)
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        tags.Add("outcome", outcome);
        tags.Add("event_type_category", category?.ToString() ?? "unclassified");
        _receivedCounter.Add(1, tags);
    }

    public void RecordInvalidSignature(StripeWebhookValidationFailure reason)
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        tags.Add("reason", reason.ToString());
        _invalidSignatureCounter.Add(1, tags);
    }

    public void RecordDuplicate(StripeWebhookEventCategory category)
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        tags.Add("event_type_category", category.ToString());
        _duplicateCounter.Add(1, tags);
    }

    public void RecordIgnored(StripeWebhookEventCategory category)
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        tags.Add("event_type_category", category.ToString());
        _ignoredCounter.Add(1, tags);
    }

    public void RecordPending()
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        _pendingCounter.Add(1, tags);
    }

    public void RecordPersistFailure()
    {
        TagList tags = default;
        tags.Add(ProviderTagName, ProviderTagValue);
        _persistFailureCounter.Add(1, tags);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
