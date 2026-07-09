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
}
