using BalanceService.IntegrationTests.Infrastructure;
using BalanceService.IntegrationTests.Infrastructure.Security;
using System.Net;
using System.Net.Http.Headers;

namespace BalanceService.IntegrationTests.Api.Security;

public sealed class BalanceAuthorizationTests : IClassFixture<BalanceApiFactory>
{
    private readonly HttpClient _client;

    public BalanceAuthorizationTests(BalanceApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_should_return_200_without_token()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Ready_should_return_200_without_token_when_db_is_available()
    {
        var res = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ready\"", body);
        Assert.Contains("\"db\":\"ok\"", body);
        Assert.DoesNotContain("\"kafka\"", body);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_without_token()
    {
        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_403_when_scope_claim_is_missing()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_403_without_balance_read_scope()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "ledger.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_when_issuer_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://invalid-issuer",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_when_audience_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_when_token_is_expired()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            now: DateTimeOffset.UtcNow.AddMinutes(-20),
            lifetimeMinutes: 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_when_signature_is_invalid()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            signWithUntrustedKey: true);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Period_endpoint_should_return_403_when_token_is_not_authorized_for_requested_merchant()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: "m2");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Daily_endpoint_should_return_200_with_balance_read_scope_and_keycloak_merchant()
    {
        var token = TestJwtTokenFactory.CreateToken();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/diario/2026-02-10?merchantId=tese");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Daily_endpoint_should_return_403_when_token_has_no_merchant_claim()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "balance-api",
            scopes: "balance.read",
            merchantIds: null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/consolidados/diario/2026-02-10?merchantId=m1");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
