using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Time;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Gateway;

namespace PaymentService.Api.Webhooks;

public sealed class StripeWebhookValidator(
    IOptions<PaymentGatewayOptions> options,
    IClock clock)
{
    private readonly IOptions<PaymentGatewayOptions> _options = options;
    private readonly IClock _clock = clock;

    public StripeWebhookValidationResult Validate(byte[] rawBody, string? signatureHeader)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        var secret = _options.Value.Stripe.WebhookSigningSecret;

        return string.IsNullOrWhiteSpace(signatureHeader)
            ? StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.MissingSignatureHeader)
            : string.IsNullOrWhiteSpace(secret)
            ? StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.MissingSecret)
            : !TryParseHeader(signatureHeader, out var timestamp, out var signatures)
            ? StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.MalformedSignatureHeader)
            : IsOutsideTolerance(timestamp, _options.Value.Stripe.WebhookSignatureTolerance)
            ? StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.TimestampOutsideTolerance)
            : !HasValidSignature(rawBody, timestamp, signatures, secret)
            ? StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.InvalidSignature)
            : TryParsePayload(rawBody, out var result)
            ? result
            : StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.InvalidPayload);
    }

    private bool IsOutsideTolerance(long timestamp, TimeSpan tolerance)
    {
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var age = (_clock.UtcNow - signedAt).Duration();
        return age > tolerance;
    }

    private static bool TryParseHeader(
        string signatureHeader,
        out long timestamp,
        out IReadOnlyCollection<string> signatures)
    {
        timestamp = 0;
        var parsedSignatures = new List<string>();

        foreach (var segment in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
                continue;

            if (string.Equals(pair[0], "t", StringComparison.Ordinal))
            {
                _ = long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out timestamp);
            }
            else if (string.Equals(pair[0], "v1", StringComparison.Ordinal))
            {
                parsedSignatures.Add(pair[1]);
            }
        }

        signatures = parsedSignatures;
        return timestamp > 0 && parsedSignatures.Count > 0;
    }

    private static bool HasValidSignature(
        byte[] rawBody,
        long timestamp,
        IEnumerable<string> signatures,
        string secret)
    {
        var timestampBytes = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture));
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var signedPayload = new byte[timestampBytes.Length + 1 + rawBody.Length];
        Buffer.BlockCopy(timestampBytes, 0, signedPayload, 0, timestampBytes.Length);
        signedPayload[timestampBytes.Length] = (byte)'.';
        Buffer.BlockCopy(rawBody, 0, signedPayload, timestampBytes.Length + 1, rawBody.Length);

        var expected = HMACSHA256.HashData(secretBytes, signedPayload);

        foreach (var signature in signatures)
        {
            if (!TryDecodeHex(signature, out var actual))
                continue;

            if (actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected))
                return true;
        }

        return false;
    }

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static bool TryParsePayload(byte[] rawBody, out StripeWebhookValidationResult result)
    {
        result = StripeWebhookValidationResult.Invalid(StripeWebhookValidationFailure.InvalidPayload);

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            if (!TryGetRequiredString(root, "id", out var eventId) ||
                !TryGetRequiredString(root, "type", out var eventType))
            {
                return false;
            }

            string? providerPaymentId = null;
            PaymentId? paymentId = null;

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("object", out var obj) &&
                obj.ValueKind == JsonValueKind.Object)
            {
                if (TryGetRequiredString(obj, "id", out var objectId))
                    providerPaymentId = objectId;

                paymentId = TryReadPaymentIdFromMetadata(obj);
            }

            result = StripeWebhookValidationResult.Valid(
                eventId,
                eventType,
                Encoding.UTF8.GetString(rawBody),
                providerPaymentId,
                paymentId);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static PaymentId? TryReadPaymentIdFromMetadata(JsonElement obj)
    {
        return !obj.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object
            ? null
            : !TryGetRequiredString(metadata, "payment_id", out var paymentIdRaw)
            ? null
            : Guid.TryParse(paymentIdRaw, out var paymentId)
            ? new PaymentId(paymentId)
            : null;
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
