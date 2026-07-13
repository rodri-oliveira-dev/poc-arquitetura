using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using PaymentService.Api.Contracts.Responses;
using PaymentService.Domain.Payments;
using PaymentService.IntegrationTests.Infrastructure;
using PaymentService.IntegrationTests.Infrastructure.Security;

namespace PaymentService.IntegrationTests.Api;

[Trait("Category", "Integration")]
public sealed class PaymentsEndpointTests(PostgresPaymentFixture fixture) : IClassFixture<PostgresPaymentFixture>, IAsyncLifetime
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
    public async Task Post_payments_should_return_202_and_persist_payment()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m1");

        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<CreatePaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.PaymentId);
        Assert.Equal("RequiresAction", body.Status);
        Assert.Equal("m1", body.MerchantId);
        Assert.Equal(100m, body.Amount);
        Assert.Equal("BRL", body.Currency);
        Assert.Equal("Stripe", body.Provider);
        Assert.StartsWith("pi_fake_", body.ProviderPaymentId, StringComparison.Ordinal);
        Assert.Equal("requires_payment_method", body.ProviderStatus);
        Assert.NotNull(body.ClientSecret);
        Assert.Equal($"/api/v1/payments/{body.PaymentId}", body.StatusUrl);
        Assert.Equal(body.StatusUrl, res.Headers.Location?.ToString());

        await using var db = _fixture.CreateDbContext();
        var saved = await db.Payments.SingleOrDefaultAsync(
            x => x.PaymentId == new PaymentId(body.PaymentId),
            TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("RequiresAction", saved.Status.ToString());
        Assert.StartsWith("pi_fake_", saved.ExternalPaymentReference?.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_payments_should_return_idempotent_response_for_same_key_and_payload()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m1");
        var key = Guid.NewGuid().ToString();

        using var firstReq = CreatePostRequest(key);
        var firstRes = await Client.SendAsync(firstReq, TestContext.Current.CancellationToken);
        var firstBody = await firstRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(firstBody);

        using var replayReq = CreatePostRequest(key);
        var replayRes = await Client.SendAsync(replayReq, TestContext.Current.CancellationToken);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(replayBody);

        Assert.Equal(HttpStatusCode.Accepted, replayRes.StatusCode);
        Assert.Equivalent(firstBody with
        {
            ClientSecret = null
        }, replayBody);
    }

    [Fact]
    public async Task Post_payments_should_return_409_for_same_key_and_different_payload()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m1");
        var key = Guid.NewGuid().ToString();

        using var firstReq = CreatePostRequest(key);
        var firstRes = await Client.SendAsync(firstReq, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, firstRes.StatusCode);

        using var conflictReq = CreatePostRequest(key, description: "Pagamento alterado");
        var conflictRes = await Client.SendAsync(conflictReq, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);
    }

    [Fact]
    public async Task Post_payments_should_return_401_without_token()
    {
        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_payments_should_return_401_without_payment_audience()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "payment.write",
            merchantIds: "m1");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Post_payments_should_return_403_without_write_scope()
    {
        Authenticate(scopes: "payment.read", merchantIds: "m1");
        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_payments_should_return_403_when_merchant_is_not_authorized()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m9");
        using var req = CreatePostRequest(Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_payments_should_return_problem_details_for_domain_validation()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m1");
        using var req = CreatePostRequest(Guid.NewGuid().ToString(), currency: "USD");

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ValidationErrorResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains("currency", body.Errors.Keys);
    }

    [Fact]
    public async Task Get_payments_should_return_200_for_authorized_merchant()
    {
        var created = await CreatePaymentAsync();
        Authenticate(scopes: "payment.read", merchantIds: "m1");

        var res = await Client.GetAsync($"/api/v1/payments/{created.PaymentId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<PaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(created.PaymentId, body.PaymentId);
        Assert.Equal("RequiresAction", body.Status);
        Assert.Equal("Stripe", body.Provider);
        Assert.StartsWith("pi_fake_", body.ProviderPaymentId, StringComparison.Ordinal);
        Assert.Null(body.LedgerEntryId);
    }

    [Fact]
    public async Task Get_payments_should_return_404_for_missing_payment()
    {
        Authenticate(scopes: "payment.read", merchantIds: "m1");

        var res = await Client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_payments_should_return_403_without_read_scope()
    {
        var created = await CreatePaymentAsync();
        Authenticate(scopes: "payment.write", merchantIds: "m1");

        var res = await Client.GetAsync($"/api/v1/payments/{created.PaymentId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Get_payments_should_return_403_for_unauthorized_merchant()
    {
        var created = await CreatePaymentAsync();
        Authenticate(scopes: "payment.read", merchantIds: "m9");

        var res = await Client.GetAsync($"/api/v1/payments/{created.PaymentId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_refunds_should_return_403_without_refund_scope()
    {
        Authenticate(scopes: "payment.write payment.read", merchantIds: "m1");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/{Guid.NewGuid()}/refunds")
        {
            Content = JsonContent.Create(new
            {
                amount = 100m,
                reason = "requested_by_customer",
                externalReference = "refund-sem-scope"
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<CreatePaymentResponse> CreatePaymentAsync()
    {
        Authenticate(scopes: "payment.write", merchantIds: "m1");
        using var req = CreatePostRequest(Guid.NewGuid().ToString());
        var res = await Client.SendAsync(req, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<CreatePaymentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private void Authenticate(string scopes, string? merchantIds = "m1")
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: TestJwtTokenFactory.PaymentAudience,
            scopes: scopes,
            merchantIds: merchantIds);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static HttpRequestMessage CreatePostRequest(
        string idempotencyKey,
        decimal amount = 100m,
        string currency = "BRL",
        string description = "Pagamento de pedido")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantId = "m1",
                amount,
                currency,
                description,
                externalReference = "pedido-123"
            })
        };

        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
    }
}
