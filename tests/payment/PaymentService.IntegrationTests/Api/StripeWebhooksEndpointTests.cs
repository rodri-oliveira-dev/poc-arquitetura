using System.Net;
using System.Text;

using Microsoft.EntityFrameworkCore;

using PaymentService.Application.Payments.Webhooks;
using PaymentService.IntegrationTests.Infrastructure;

namespace PaymentService.IntegrationTests.Api;

[Trait("Category", "Integration")]
public sealed class StripeWebhooksEndpointTests(PostgresPaymentFixture fixture) : IClassFixture<PostgresPaymentFixture>, IAsyncLifetime
{
    private readonly PostgresPaymentFixture _fixture = fixture;
    private PostgresPaymentApiFactory? _factory;

    private HttpClient Client
    {
        get => field ?? throw new InvalidOperationException("Client nao inicializado.");
        set;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
        _factory = new PostgresPaymentApiFactory(_fixture.ConnectionString);
        Client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory.Dispose();
        }
    }

    [Fact]
    public async Task Webhook_should_accept_valid_signature_without_jwt_and_persist_inbox()
    {
        var paymentId = Guid.NewGuid();
        var payload = StripeWebhookTestData.CreatePayload(paymentId: paymentId);

        var res = await SendWebhookAsync(payload);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await using var db = _fixture.CreateDbContext();
        var saved = await db.InboxMessages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("evt_test_123", saved.ProviderEventId);
        Assert.Equal("payment_intent.succeeded", saved.EventType);
        Assert.Equal(PaymentInboxStatus.Pending, saved.Status);
        Assert.Equal(StripeWebhookEventCategory.Supported, saved.EventCategory);
        Assert.Equal("pi_test_123", saved.ProviderPaymentId);
        Assert.Equal(paymentId, saved.PaymentId?.Value);
        Assert.Equal(payload, saved.Payload);
    }

    [Fact]
    public async Task Webhook_should_return_success_for_duplicate_event_without_second_row()
    {
        var payload = StripeWebhookTestData.CreatePayload(eventId: "evt_duplicate_http");

        var first = await SendWebhookAsync(payload);
        var second = await SendWebhookAsync(payload);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(1, await db.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Webhook_should_reject_invalid_signature_without_persisting()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        using var request = CreateWebhookRequest(payload, "t=1,v1=bad");

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_reject_missing_signature_without_persisting()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_reject_payload_changed_after_signature()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        var tamperedPayload = payload.Replace("payment_intent.succeeded", "payment_intent.processing", StringComparison.Ordinal);
        using var request = CreateWebhookRequest(tamperedPayload, StripeWebhookTestData.CreateSignatureHeader(payload));

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_reject_old_timestamp()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload, oldTimestamp));

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_reject_signature_created_with_wrong_secret()
    {
        var payload = StripeWebhookTestData.CreatePayload();
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload, secret: "whsec_wrong"));

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_reject_invalid_json_after_valid_signature()
    {
        const string payload = "{\"id\":\"evt_bad\"";
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload));

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Theory]
    [InlineData("charge.dispute.created", StripeWebhookEventCategory.KnownUnsupported)]
    [InlineData("treasury.received_credit.created", StripeWebhookEventCategory.Unknown)]
    public async Task Webhook_should_persist_non_mvp_events_as_ignored(
        string eventType,
        StripeWebhookEventCategory expectedCategory)
    {
        var payload = StripeWebhookTestData.CreatePayload(eventId: $"evt_{Guid.NewGuid():N}", eventType: eventType);

        var res = await SendWebhookAsync(payload);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await using var db = _fixture.CreateDbContext();
        var saved = await db.InboxMessages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentInboxStatus.Ignored, saved.Status);
        Assert.Equal(expectedCategory, saved.EventCategory);
    }

    [Fact]
    public async Task Webhook_should_reject_payload_above_configured_limit()
    {
        var payload = StripeWebhookTestData.CreatePayload(paymentIntentId: new string('x', 1500));
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload));

        var res = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        await AssertInboxCountAsync(0);
    }

    [Fact]
    public async Task Webhook_should_deduplicate_concurrent_http_requests()
    {
        const int requests = 20;
        var payload = StripeWebhookTestData.CreatePayload(eventId: "evt_concurrent_http");

        var responses = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => SendWebhookAsync(payload)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(1, await db.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Webhook_should_not_return_success_when_database_is_unavailable()
    {
        await using var unavailableFactory = new PostgresPaymentApiFactory(
            "Host=127.0.0.1;Port=1;Database=appdb;Username=appuser;Password=app123;Timeout=1;Command Timeout=1");
        using var client = unavailableFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var payload = StripeWebhookTestData.CreatePayload(eventId: "evt_db_down");
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload));

        var res = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string payload)
    {
        using var request = CreateWebhookRequest(payload, StripeWebhookTestData.CreateSignatureHeader(payload));
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage CreateWebhookRequest(string payload, string signatureHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", signatureHeader);
        return request;
    }

    private async Task AssertInboxCountAsync(int expected)
    {
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(expected, await db.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }
}
