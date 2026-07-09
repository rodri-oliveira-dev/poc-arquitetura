using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentService.IntegrationTests.Infrastructure;

internal static class StripeWebhookTestData
{
    public const string Secret = "whsec_test_secret";

    public static string CreatePayload(
        string eventId = "evt_test_123",
        string eventType = "payment_intent.succeeded",
        string paymentIntentId = "pi_test_123",
        Guid? paymentId = null)
    {
        Dictionary<string, string> metadata = [];
        if (paymentId is not null)
            metadata["payment_id"] = paymentId.Value.ToString();

        return JsonSerializer.Serialize(new
        {
            id = eventId,
            @object = "event",
            type = eventType,
            data = new
            {
                @object = new
                {
                    id = paymentIntentId,
                    @object = "payment_intent",
                    metadata
                }
            }
        });
    }

    public static string CreateSignatureHeader(string payload, DateTimeOffset? timestamp = null, string secret = Secret)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedPayload));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"t={ts},v1={Convert.ToHexString(signature).ToLowerInvariant()}");
    }
}
