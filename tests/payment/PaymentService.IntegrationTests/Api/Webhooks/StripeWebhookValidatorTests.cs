using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using PaymentService.Api.Webhooks;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Gateway;

namespace PaymentService.IntegrationTests.Api.Webhooks;

public sealed class StripeWebhookValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private const string Secret = "whsec_test_secret";

    [Fact]
    public void Validate_should_reject_missing_signature_header()
    {
        var validator = CreateValidator();

        var result = validator.Validate(ValidPayload(), signatureHeader: null);

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.MissingSignatureHeader, result.Failure);
    }

    [Fact]
    public void Validate_should_reject_missing_webhook_secret()
    {
        var validator = CreateValidator(secret: string.Empty);

        var result = validator.Validate(ValidPayload(), SignatureHeader(ValidPayload()));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.MissingSecret, result.Failure);
    }

    [Theory]
    [InlineData("v1=abcdef")]
    [InlineData("t=123")]
    [InlineData("not-a-stripe-signature")]
    public void Validate_should_reject_malformed_signature_header(string signatureHeader)
    {
        ArgumentNullException.ThrowIfNull(signatureHeader);
        var validator = CreateValidator();

        var result = validator.Validate(ValidPayload(), signatureHeader);

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.MalformedSignatureHeader, result.Failure);
    }

    [Fact]
    public void Validate_should_reject_invalid_signature()
    {
        var payload = ValidPayload();
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload, signature: "00"));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.InvalidSignature, result.Failure);
    }

    [Fact]
    public void Validate_should_ignore_non_hex_signature_and_reject_when_no_valid_signature_remains()
    {
        var payload = ValidPayload();
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload, signature: "not-hex"));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.InvalidSignature, result.Failure);
    }

    [Fact]
    public void Validate_should_reject_tampered_payload()
    {
        var original = ValidPayload();
        var tampered = ValidPayload("pi_tampered");
        var validator = CreateValidator();

        var result = validator.Validate(tampered, SignatureHeader(original));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.InvalidSignature, result.Failure);
    }

    [Fact]
    public void Validate_should_reject_timestamp_outside_tolerance()
    {
        var payload = ValidPayload();
        var validator = CreateValidator();
        var oldTimestamp = Now.AddMinutes(-10).ToUnixTimeSeconds();

        var result = validator.Validate(payload, SignatureHeader(payload, oldTimestamp));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.TimestampOutsideTolerance, result.Failure);
    }

    [Fact]
    public void Validate_should_accept_supported_event_and_extract_payment_metadata()
    {
        var paymentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var payload = ValidPayload(paymentId: paymentId);
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.True(result.IsValid);
        Assert.Equal("evt_123", result.ProviderEventId);
        Assert.Equal("payment_intent.succeeded", result.EventType);
        Assert.Equal("pi_123", result.ProviderPaymentId);
        Assert.Equal(new PaymentId(paymentId), result.PaymentId);
        Assert.Equal(Encoding.UTF8.GetString(payload), result.RawPayload);
    }

    [Fact]
    public void Validate_should_accept_unknown_valid_event_without_payment_metadata()
    {
        var payload = ValidPayload(eventType: "treasury.received_credit.created", paymentId: null);
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.True(result.IsValid);
        Assert.Equal("treasury.received_credit.created", result.EventType);
        Assert.Equal("pi_123", result.ProviderPaymentId);
        Assert.Null(result.PaymentId);
    }

    [Fact]
    public void Validate_should_accept_valid_event_without_object_payload()
    {
        var payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"id\":\"evt_123\",\"type\":\"payment_intent.succeeded\"}");
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.True(result.IsValid);
        Assert.Equal("evt_123", result.ProviderEventId);
        Assert.Equal("payment_intent.succeeded", result.EventType);
        Assert.Null(result.ProviderPaymentId);
        Assert.Null(result.PaymentId);
    }

    [Fact]
    public void Validate_should_accept_event_with_invalid_internal_payment_metadata_without_binding_payment()
    {
        var payload = ValidPayload(paymentIdRaw: "not-a-guid");
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.True(result.IsValid);
        Assert.Equal("pi_123", result.ProviderPaymentId);
        Assert.Null(result.PaymentId);
    }

    [Fact]
    public void Validate_should_reject_invalid_json_payload()
    {
        var payload = Encoding.UTF8.GetBytes("{invalid-json");
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.InvalidPayload, result.Failure);
    }

    [Fact]
    public void Validate_should_reject_json_payload_without_required_event_fields()
    {
        var payload = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"data\":{\"object\":{\"id\":\"pi_123\"}}}");
        var validator = CreateValidator();

        var result = validator.Validate(payload, SignatureHeader(payload));

        Assert.False(result.IsValid);
        Assert.Equal(StripeWebhookValidationFailure.InvalidPayload, result.Failure);
    }

    private static StripeWebhookValidator CreateValidator(string secret = Secret)
        => new(
            Options.Create(new PaymentGatewayOptions
            {
                Stripe = new StripePaymentGatewayOptions
                {
                    WebhookSigningSecret = secret,
                    WebhookSignatureTolerance = TimeSpan.FromMinutes(5)
                }
            }),
            new FixedClock(Now));

    private static byte[] ValidPayload(
        string providerPaymentId = "pi_123",
        string eventType = "payment_intent.succeeded",
        Guid? paymentId = null,
        string? paymentIdRaw = null)
    {
        var metadataPaymentId = paymentIdRaw ?? paymentId?.ToString("D");
        var paymentIdJson = metadataPaymentId is null
            ? string.Empty
            : $",\"metadata\":{{\"payment_id\":\"{metadataPaymentId}\"}}";

        return Encoding.UTF8.GetBytes(
            "{\"id\":\"evt_123\",\"type\":\"" + eventType + "\",\"data\":{\"object\":{\"id\":\"" + providerPaymentId + "\"" + paymentIdJson + "}}}");
    }

    private static string SignatureHeader(byte[] payload, long? timestamp = null, string? signature = null)
    {
        var signedAt = timestamp ?? Now.ToUnixTimeSeconds();
        var computedSignature = signature ?? ComputeSignature(payload, signedAt);
        return string.Create(CultureInfo.InvariantCulture, $"t={signedAt},v1={computedSignature}");
    }

    private static string ComputeSignature(byte[] payload, long timestamp)
    {
        var signedPayload = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{timestamp}.{Encoding.UTF8.GetString(payload)}"));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), signedPayload);
        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
