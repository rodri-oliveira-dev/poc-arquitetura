using System.Net;

using Microsoft.Extensions.Options;

using TransferService.Worker.Ledger;
using TransferService.Worker.Options;
using TransferService.Worker.Tests.Support;

namespace TransferService.Worker.Tests.Ledger;

public sealed class ClientCredentialsLedgerAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_should_request_client_credentials_token_with_configured_scope_Async()
    {
        using var fixture = new TokenProviderFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-1", "expires_in": 600 }""");

        var token = await fixture.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", token);
        Assert.Equal(HttpMethod.Post, fixture.Handler.LastRequest?.Method);
        Assert.Equal("/realms/poc/protocol/openid-connect/token", fixture.Handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("grant_type=client_credentials", fixture.Handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("client_id=poc-automation", fixture.Handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("client_secret=local-secret", fixture.Handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("scope=ledger.write", fixture.Handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_should_reuse_token_until_refresh_skew_Async()
    {
        using var fixture = new TokenProviderFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-1", "expires_in": 600 }""");

        var first = await fixture.Provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await fixture.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(0, fixture.Handler.PendingResponses);
    }

    [Fact]
    public async Task GetAccessTokenAsync_should_fail_with_clear_message_when_token_endpoint_rejects_credentials_Async()
    {
        using var fixture = new TokenProviderFixture();
        fixture.Handler.Enqueue(HttpStatusCode.Unauthorized, "invalid_client");

        var exception = await Assert.ThrowsAsync<LedgerAuthenticationException>(
            () => fixture.Provider.GetAccessTokenAsync(CancellationToken.None).AsTask());

        Assert.Contains("Falha ao obter token", exception.Message, StringComparison.Ordinal);
        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid_client", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TokenProviderFixture : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TokenProviderFixture()
        {
            Handler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri("http://keycloak:8080")
            };

            var workerOptions = new TransferWorkerOptions
            {
                Ledger =
                {
                    Auth =
                    {
                        TokenEndpoint = new Uri("http://keycloak:8080/realms/poc/protocol/openid-connect/token"),
                        ClientId = "poc-automation",
                        ClientSecret = "local-secret",
                        Scope = "ledger.write",
                        RefreshSkew = TimeSpan.FromMinutes(1)
                    }
                }
            };

            Provider = new ClientCredentialsLedgerAccessTokenProvider(
                _httpClient,
                new StaticOptionsMonitor<TransferWorkerOptions>(workerOptions),
                TimeProvider.System);
        }

        public FakeHttpMessageHandler Handler
        {
            get;
        }

        public ClientCredentialsLedgerAccessTokenProvider Provider
        {
            get;
        }

        public void Dispose()
            => _httpClient.Dispose();
    }

    private sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue
        {
            get;
        } = value;

        public TOptions Get(string? name)
            => CurrentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener)
            => null;
    }
}
