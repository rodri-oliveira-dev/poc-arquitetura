using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Gateway;
using PaymentService.Infrastructure.Gateway;

namespace PaymentService.UnitTests.Infrastructure.Gateway;

public sealed class FakePaymentGatewayTests
{
    private static readonly Guid PaymentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RefundId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 12, 00, 00, TimeSpan.Zero);

    [Theory]
    [InlineData(FakePaymentGatewayScenarios.Success, "requires_payment_method", true)]
    [InlineData(FakePaymentGatewayScenarios.RequiresAction, "requires_action", true)]
    [InlineData(FakePaymentGatewayScenarios.Processing, "processing", false)]
    public async Task CreatePaymentIntentAsync_should_return_configured_success_scenario(
        string scenario,
        string expectedStatus,
        bool expectedRequiresAction)
    {
        using var fixture = CreateGateway(scenario);

        var result = await fixture.Gateway.CreatePaymentIntentAsync(PaymentRequest(), CancellationToken.None);

        Assert.Equal("Fake", result.Provider);
        Assert.StartsWith("pi_fake_", result.ExternalPaymentReference, StringComparison.Ordinal);
        Assert.Equal(expectedStatus, result.ProviderStatus);
        Assert.Equal(expectedRequiresAction, result.RequiresAction);
        Assert.Equal(expectedStatus, result.RawStatus);
    }

    [Theory]
    [InlineData(FakePaymentGatewayScenarios.DefinitiveFailure, PaymentGatewayErrorCategory.PaymentRejected, "fake_payment_rejected")]
    [InlineData(FakePaymentGatewayScenarios.Timeout, PaymentGatewayErrorCategory.UnknownResult, "fake_timeout")]
    [InlineData(FakePaymentGatewayScenarios.RateLimit, PaymentGatewayErrorCategory.RateLimited, "fake_rate_limited")]
    [InlineData(FakePaymentGatewayScenarios.TransientFailure, PaymentGatewayErrorCategory.Transient, "fake_transient")]
    public async Task CreatePaymentIntentAsync_should_throw_configured_failure(
        string scenario,
        PaymentGatewayErrorCategory expectedCategory,
        string expectedCode)
    {
        using var fixture = CreateGateway(scenario);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => fixture.Gateway.CreatePaymentIntentAsync(PaymentRequest(), CancellationToken.None));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal(expectedCode, exception.Code);
    }

    [Theory]
    [InlineData(FakePaymentGatewayScenarios.Success, "succeeded")]
    [InlineData(FakePaymentGatewayScenarios.RefundPending, "pending")]
    public async Task CreateRefundAsync_should_return_configured_refund_success(string scenario, string expectedStatus)
    {
        using var fixture = CreateGateway(scenario);

        var result = await fixture.Gateway.CreateRefundAsync(RefundRequest(), CancellationToken.None);

        Assert.Equal("Fake", result.Provider);
        Assert.StartsWith("re_fake_", result.ProviderRefundId, StringComparison.Ordinal);
        Assert.Equal("pi_original", result.ProviderPaymentId);
        Assert.Equal(expectedStatus, result.ProviderStatus);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal(Now, result.CreatedAt);
    }

    [Theory]
    [InlineData(FakePaymentGatewayScenarios.Timeout, PaymentGatewayErrorCategory.UnknownResult, "fake_refund_timeout")]
    [InlineData(FakePaymentGatewayScenarios.RateLimit, PaymentGatewayErrorCategory.RateLimited, "fake_refund_rate_limited")]
    [InlineData(FakePaymentGatewayScenarios.TransientFailure, PaymentGatewayErrorCategory.Transient, "fake_refund_transient")]
    [InlineData(FakePaymentGatewayScenarios.RefundFailed, PaymentGatewayErrorCategory.InvalidRequest, "fake_refund_failed")]
    public async Task CreateRefundAsync_should_throw_configured_refund_failure(
        string scenario,
        PaymentGatewayErrorCategory expectedCategory,
        string expectedCode)
    {
        using var fixture = CreateGateway(scenario);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => fixture.Gateway.CreateRefundAsync(RefundRequest(), CancellationToken.None));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal(expectedCode, exception.Code);
    }

    private static FakeGatewayFixture CreateGateway(string scenario)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var telemetry = new PaymentGatewayTelemetry(provider.GetRequiredService<IMeterFactory>());
        var gateway = new FakePaymentGateway(
            Options.Create(new PaymentGatewayOptions
            {
                Fake = new FakePaymentGatewayOptions
                {
                    Scenario = scenario
                }
            }),
            telemetry,
            new FixedClock(Now),
            NullLogger<FakePaymentGateway>.Instance);
        return new FakeGatewayFixture(gateway, telemetry, provider);
    }

    private static CreateExternalPaymentRequest PaymentRequest()
        => new(
            PaymentId,
            "merchant-001",
            100m,
            "BRL",
            "Pagamento fake",
            "order-123",
            "payment-key",
            "correlation-1");

    private static CreateExternalRefundRequest RefundRequest()
        => new(
            PaymentId,
            RefundId,
            "pi_original",
            100m,
            "BRL",
            "requested_by_customer",
            "refund-key",
            "correlation-1",
            "refund-ext");

    private sealed class FakeGatewayFixture(
        FakePaymentGateway gateway,
        PaymentGatewayTelemetry telemetry,
        ServiceProvider serviceProvider) : IDisposable
    {
        public FakePaymentGateway Gateway { get; } = gateway;

        public void Dispose()
        {
            telemetry.Dispose();
            serviceProvider.Dispose();
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
