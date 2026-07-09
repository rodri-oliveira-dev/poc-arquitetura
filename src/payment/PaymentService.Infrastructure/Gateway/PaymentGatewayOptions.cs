namespace PaymentService.Infrastructure.Gateway;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string Provider { get; init; } = PaymentGatewayProviders.Fake;

    public FakePaymentGatewayOptions Fake { get; init; } = new();

    public StripePaymentGatewayOptions Stripe { get; init; } = new();
}
