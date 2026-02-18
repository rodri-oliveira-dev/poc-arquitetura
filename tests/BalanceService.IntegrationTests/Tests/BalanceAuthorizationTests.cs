using BalanceService.IntegrationTests.Infrastructure;
using BalanceService.IntegrationTests.Infrastructure.Security;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;

namespace BalanceService.IntegrationTests.Tests;

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
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Period_endpoint_should_return_401_without_token()
    {
        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Period_endpoint_should_return_403_without_balance_read_scope()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "ledger.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-10&to=2026-02-12");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
