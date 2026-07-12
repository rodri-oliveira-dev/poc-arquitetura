using System.Net;
using System.Text.Json;

using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Ledger;

namespace PaymentService.IntegrationTests.Infrastructure.Gateway;

public sealed class LedgerHttpGatewayTests
{
    [Fact]
    public async Task CreateCreditAsync_should_post_credit_with_idempotency_and_correlation_headers()
    {
        using var fixture = new LedgerGatewayFixture();
        var ledgerEntryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        fixture.Handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{ledgerEntryId}}" }""");

        var result = await fixture.Gateway.CreateCreditAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(LedgerEntryCreationOutcome.Accepted, result.Outcome);
        Assert.Equal(ledgerEntryId, result.LedgerEntryReference?.Value);
        Assert.Equal(HttpMethod.Post, fixture.Handler.LastRequest?.Method);
        Assert.Equal("/api/v1/lancamentos", fixture.Handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Equal("aaaaaaaa-1111-5111-9111-aaaaaaaaaaaa", fixture.Handler.LastRequest?.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", fixture.Handler.LastRequest?.Headers.GetValues("X-Correlation-Id").Single());

        using var document = JsonDocument.Parse(fixture.Handler.LastRequestBody!);
        Assert.Equal("merchant-001", document.RootElement.GetProperty("merchantId").GetString());
        Assert.Equal("CREDIT", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(100m, document.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Payment captured", document.RootElement.GetProperty("description").GetString());
        Assert.Equal("payment:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", document.RootElement.GetProperty("externalReference").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, LedgerEntryCreationOutcome.DefinitiveFailure, LedgerEntryFailureCategory.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, LedgerEntryCreationOutcome.DefinitiveFailure, LedgerEntryFailureCategory.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, LedgerEntryCreationOutcome.DefinitiveFailure, LedgerEntryFailureCategory.Authorization)]
    [InlineData(HttpStatusCode.Conflict, LedgerEntryCreationOutcome.DefinitiveFailure, LedgerEntryFailureCategory.IdempotencyConflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity, LedgerEntryCreationOutcome.DefinitiveFailure, LedgerEntryFailureCategory.Validation)]
    [InlineData(HttpStatusCode.TooManyRequests, LedgerEntryCreationOutcome.TransientFailure, LedgerEntryFailureCategory.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, LedgerEntryCreationOutcome.TransientFailure, LedgerEntryFailureCategory.ServiceUnavailable)]
    public async Task CreateCreditAsync_should_classify_http_failures(
        HttpStatusCode statusCode,
        LedgerEntryCreationOutcome expectedOutcome,
        LedgerEntryFailureCategory expectedCategory)
    {
        using var fixture = new LedgerGatewayFixture();
        fixture.Handler.Enqueue(statusCode, "{}");

        var result = await fixture.Gateway.CreateCreditAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedCategory, result.FailureCategory);
    }

    [Fact]
    public async Task CreateCreditAsync_should_not_persist_raw_error_body_from_ledger()
    {
        using var fixture = new LedgerGatewayFixture();
        fixture.Handler.Enqueue(
            HttpStatusCode.BadRequest,
            /*lang=json,strict*/ """{ "detail": "stack trace or sensitive downstream payload" }""");

        var result = await fixture.Gateway.CreateCreditAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("stack trace", result.SafeError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive downstream payload", result.SafeError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("LedgerService.Api returned HTTP 400 (BadRequest).", result.SafeError);
    }

    private static LedgerCreditRequest CreateRequest()
        => new(
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new MerchantId("merchant-001"),
            new Money(100m, Currency.Brl),
            "Payment captured",
            "payment:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            Guid.Parse("aaaaaaaa-1111-5111-9111-aaaaaaaaaaaa"),
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private sealed class LedgerGatewayFixture : IDisposable
    {
        private readonly HttpClient _httpClient;

        public LedgerGatewayFixture()
        {
            Handler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri("https://ledger.local/")
            };
            Gateway = new LedgerHttpGateway(_httpClient);
        }

        public FakeHttpMessageHandler Handler
        {
            get;
        }

        public LedgerHttpGateway Gateway
        {
            get;
        }

        public void Dispose()
            => _httpClient.Dispose();
    }
}
