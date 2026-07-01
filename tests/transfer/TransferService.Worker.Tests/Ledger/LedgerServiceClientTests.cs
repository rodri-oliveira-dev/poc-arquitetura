using System.Net;
using System.Text.Json;

using TransferService.Worker.Ledger;
using TransferService.Worker.Tests.Support;

namespace TransferService.Worker.Tests.Ledger;

public sealed class LedgerServiceClientTests
{
    [Fact]
    public async Task CreateLancamentoAsync_should_post_payload_with_idempotency_and_correlation_headers_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        var lancamentoId = Guid.NewGuid();
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{lancamentoId}}" }""");

        var result = await fixture.Client.CreateLancamentoAsync(
            new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, "Debito transferencia", "transfer-1"),
            "idem-1",
            "correlation-1",
            CancellationToken.None);

        Assert.Equal(lancamentoId, result.LancamentoId);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/lancamentos", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Equal("idem-1", Header(handler, "Idempotency-Key"));
        Assert.Equal("correlation-1", Header(handler, "X-Correlation-Id"));

        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("merchant-1", document.RootElement.GetProperty("merchantId").GetString());
        Assert.Equal("DEBIT", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(-100m, document.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Debito transferencia", document.RootElement.GetProperty("description").GetString());
        Assert.Equal("transfer-1", document.RootElement.GetProperty("externalReference").GetString());
    }

    [Fact]
    public async Task CreateLancamentoAsync_should_send_bearer_token_when_auth_handler_is_configured_Async()
    {
        using var fixture = new AuthenticatedLedgerClientFixture("access-token-1");
        var handler = fixture.Handler;
        var lancamentoId = Guid.NewGuid();
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{lancamentoId}}" }""");

        await fixture.Client.CreateLancamentoAsync(
            new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, "Debito transferencia", "transfer-1"),
            "idem-1",
            "correlation-1",
            CancellationToken.None);

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("idem-1", Header(handler, "Idempotency-Key"));
        Assert.Equal("correlation-1", Header(handler, "X-Correlation-Id"));
    }

    [Fact]
    public async Task CreateLancamentoAsync_should_parse_legacy_id_property_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        var lancamentoId = Guid.NewGuid();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""{ "id": "{{lancamentoId}}" }""");

        var result = await fixture.Client.CreateLancamentoAsync(
            new CreateLedgerLancamentoRequest("merchant-1", "CREDIT", 100m, null, null),
            "idem-1",
            null,
            CancellationToken.None);

        Assert.Equal(lancamentoId, result.LancamentoId);
        Assert.False(handler.LastRequest!.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task SolicitarEstornoAsync_should_post_payload_and_return_estorno_id_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        var lancamentoId = Guid.NewGuid();
        var estornoId = Guid.NewGuid();
        handler.EnqueueJson(HttpStatusCode.Accepted, $$"""{ "estornoId": "{{estornoId}}" }""");

        var result = await fixture.Client.SolicitarEstornoAsync(
            lancamentoId,
            new SolicitarLedgerEstornoRequest("Compensacao de transferencia"),
            "idem-estorno",
            "correlation-2",
            CancellationToken.None);

        Assert.Equal(estornoId, result.EstornoId);
        Assert.Equal($"/api/v1/lancamentos/{lancamentoId}/estornos", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Equal("idem-estorno", Header(handler, "Idempotency-Key"));
        Assert.Equal("correlation-2", Header(handler, "X-Correlation-Id"));
        Assert.Contains("Compensacao de transferencia", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLancamentoAsync_should_throw_ledger_exception_on_http_error_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        handler.Enqueue(HttpStatusCode.BadRequest, "valor invalido");

        var exception = await Assert.ThrowsAsync<LedgerServiceException>(
            () => fixture.Client.CreateLancamentoAsync(
                new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, null, null),
                "idem-1",
                null,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("valor invalido", exception.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLancamentoAsync_should_report_unauthorized_as_service_to_service_authentication_failure_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        handler.Enqueue(HttpStatusCode.Unauthorized, "token invalido");

        var exception = await Assert.ThrowsAsync<LedgerServiceException>(
            () => fixture.Client.CreateLancamentoAsync(
                new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, null, null),
                "idem-1",
                null,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("service-to-service", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token invalido", exception.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLancamentoAsync_should_observe_cancellation_token_Async()
    {
        using var fixture = new LedgerClientFixture();
        var handler = fixture.Handler;
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Client.CreateLancamentoAsync(
                new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, null, null),
                "idem-1",
                null,
                cts.Token));
    }

    [Fact]
    public async Task Public_members_should_validate_required_arguments_Async()
    {
        using var fixture = new LedgerClientFixture();

        Assert.Throws<ArgumentNullException>(() => new LedgerServiceClient(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Client.CreateLancamentoAsync(null!, "idem", null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Client.CreateLancamentoAsync(
            new CreateLedgerLancamentoRequest("merchant-1", "DEBIT", -100m, null, null),
            null!,
            null,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Client.SolicitarEstornoAsync(
            Guid.NewGuid(),
            null!,
            "idem",
            null,
            CancellationToken.None));
    }

    private sealed class LedgerClientFixture : IDisposable
    {
        private readonly HttpClient _httpClient;

        public LedgerClientFixture()
        {
            Handler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri("https://ledger.local/")
            };
            Client = new LedgerServiceClient(_httpClient);
        }

        public FakeHttpMessageHandler Handler
        {
            get;
        }

        public LedgerServiceClient Client
        {
            get;
        }

        public void Dispose()
            => _httpClient.Dispose();
    }

    private sealed class AuthenticatedLedgerClientFixture : IDisposable
    {
        private readonly LedgerAuthenticationHandler _authHandler;
        private readonly HttpClient _httpClient;

        public AuthenticatedLedgerClientFixture(string accessToken)
        {
            Handler = new FakeHttpMessageHandler();
            _authHandler = new LedgerAuthenticationHandler(new StubLedgerAccessTokenProvider(accessToken))
            {
                InnerHandler = Handler
            };
            _httpClient = new HttpClient(_authHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://ledger.local/")
            };
            Client = new LedgerServiceClient(_httpClient);
        }

        public FakeHttpMessageHandler Handler
        {
            get;
        }

        public LedgerServiceClient Client
        {
            get;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _authHandler.Dispose();
        }
    }

    private sealed class StubLedgerAccessTokenProvider(string accessToken) : ILedgerAccessTokenProvider
    {
        private readonly string _accessToken = accessToken;

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_accessToken);
    }

    private static string Header(FakeHttpMessageHandler handler, string name)
        => handler.LastRequest!.Headers.GetValues(name).Single();
}
