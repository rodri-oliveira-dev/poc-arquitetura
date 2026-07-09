namespace PaymentService.Infrastructure.Gateway;

public sealed class FakePaymentGatewayOptions
{
    public string Scenario { get; init; } = FakePaymentGatewayScenarios.Success;

    public string ProviderPaymentIdPrefix { get; init; } = "pi_fake";

    public TimeSpan SimulatedDelay { get; init; } = TimeSpan.Zero;
}
