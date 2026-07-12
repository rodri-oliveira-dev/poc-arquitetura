namespace PaymentService.Infrastructure.Gateway;

public sealed class StripePaymentGatewayOptions
{
    public string SecretKey { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public Uri ApiBaseUrl { get; init; } = new("https://api.stripe.com/v1/");

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public string WebhookSigningSecret { get; init; } = string.Empty;

    public TimeSpan WebhookSignatureTolerance { get; init; } = TimeSpan.FromMinutes(5);

    public string EffectiveSecretKey
        => string.IsNullOrWhiteSpace(SecretKey) ? ApiKey : SecretKey;
}
