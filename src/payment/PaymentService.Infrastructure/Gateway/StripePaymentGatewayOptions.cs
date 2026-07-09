namespace PaymentService.Infrastructure.Gateway;

public sealed class StripePaymentGatewayOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public Uri ApiBaseUrl { get; init; } = new("https://api.stripe.com/v1/");

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
