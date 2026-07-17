using System.Diagnostics.Metrics;
using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Gateway;
using PaymentService.Infrastructure.Gateway;

namespace PaymentService.IntegrationTests.Infrastructure.Gateway;

public sealed class StripePaymentGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task CreatePaymentIntentAsync_should_send_expected_stripe_request()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            /*lang=json,strict*/ """{ "id": "pi_123", "status": "requires_payment_method", "client_secret": "secret_123" }""");
        using var fixture = CreateGateway(handler);
        var request = Request();

        var result = await fixture.Gateway.CreatePaymentIntentAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Stripe", result.Provider);
        Assert.Equal("pi_123", result.ExternalPaymentReference);
        Assert.Equal("requires_payment_method", result.ProviderStatus);
        Assert.True(result.RequiresAction);
        Assert.Equal("secret_123", result.ClientSecret);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("https://stripe.test/v1/payment_intents", handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal("Bearer stripe-secret-placeholder", handler.LastRequest?.Headers.Authorization?.ToString());
        IEnumerable<string>? values = null;
        var hasIdempotencyKey = handler.LastRequest?.Headers.TryGetValues("Idempotency-Key", out values) == true;
        Assert.True(hasIdempotencyKey);
        Assert.Equal("payment-idempotency-key", Assert.Single(values!));
        Assert.Contains("amount=10000", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("currency=brl", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("automatic_payment_methods%5Benabled%5D=true", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("metadata%5Bpayment_id%5D=11111111-1111-1111-1111-111111111111", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("metadata%5Bmerchant_id%5D=m1", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("metadata%5Bexternal_reference%5D=order-123", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, PaymentGatewayErrorCategory.RateLimited)]
    [InlineData(HttpStatusCode.Unauthorized, PaymentGatewayErrorCategory.AuthenticationFailed)]
    [InlineData(HttpStatusCode.BadRequest, PaymentGatewayErrorCategory.InvalidRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PaymentGatewayErrorCategory.Transient)]
    public async Task CreatePaymentIntentAsync_should_map_stripe_errors(HttpStatusCode statusCode, PaymentGatewayErrorCategory category)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(statusCode, /*lang=json,strict*/ """{ "error": { "code": "stripe_error_code" } }""");
        using var fixture = CreateGateway(handler);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => fixture.Gateway.CreatePaymentIntentAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(category, exception.Category);
        Assert.Equal("stripe_error_code", exception.Code);
    }

    [Fact]
    public async Task CreateRefundAsync_should_preserve_stripe_created_timestamp()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            /*lang=json,strict*/ """{ "id": "re_123", "status": "succeeded", "payment_intent": "pi_123", "amount": 10000, "currency": "brl", "created": 1783598400 }""");
        using var fixture = CreateGateway(handler);

        var result = await fixture.Gateway.CreateRefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783598400), result.CreatedAt);
    }

    [Fact]
    public async Task CreateRefundAsync_should_use_controlled_fallback_when_stripe_omits_created()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            /*lang=json,strict*/ """{ "id": "re_123", "status": "succeeded", "payment_intent": "pi_123", "amount": 10000, "currency": "brl" }""");
        using var fixture = CreateGateway(handler);

        var result = await fixture.Gateway.CreateRefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(Now, result.CreatedAt);
    }

    private static StripeGatewayFixture CreateGateway(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://stripe.test/v1/")
        };

        var telemetry = new PaymentGatewayTelemetry(provider.GetRequiredService<IMeterFactory>());
        var gateway = new StripePaymentGateway(
            httpClient,
            Options.Create(new PaymentGatewayOptions
            {
                Provider = PaymentGatewayProviders.Stripe,
                Stripe = new StripePaymentGatewayOptions
                {
                    ApiKey = "stripe-secret-placeholder",
                    ApiBaseUrl = new Uri("https://stripe.test/v1/"),
                    Timeout = TimeSpan.FromSeconds(10)
                }
            }),
            telemetry,
            new FixedClock(Now),
            NullLogger<StripePaymentGateway>.Instance);

        return new StripeGatewayFixture(gateway, httpClient, telemetry, provider);
    }

    private static CreateExternalPaymentRequest Request()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "m1",
            100m,
            "BRL",
            "Pagamento pedido",
            "order-123",
            "payment-idempotency-key",
            "corr-1");

    private static CreateExternalRefundRequest RefundRequest()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "pi_123",
            100m,
            "BRL",
            "requested_by_customer",
            "refund-idempotency-key",
            "corr-1",
            "refund-ext");

    private sealed class StripeGatewayFixture(
        StripePaymentGateway gateway,
        HttpClient httpClient,
        PaymentGatewayTelemetry telemetry,
        ServiceProvider serviceProvider) : IDisposable
    {
        public StripePaymentGateway Gateway { get; } = gateway;

        public void Dispose()
        {
            httpClient.Dispose();
            telemetry.Dispose();
            serviceProvider.Dispose();
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
