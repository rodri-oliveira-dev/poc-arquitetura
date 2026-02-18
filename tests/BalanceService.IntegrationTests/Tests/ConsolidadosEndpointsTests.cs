using BalanceService.IntegrationTests.Infrastructure;
using BalanceService.IntegrationTests.Infrastructure.Security;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;

namespace BalanceService.IntegrationTests.Tests;

public sealed class ConsolidadosEndpointsTests : IClassFixture<BalanceApiFactory>
{
    private readonly HttpClient _client;

    public ConsolidadosEndpointsTests(BalanceApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Period_should_return_400_when_from_invalid_format()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=bad&to=2026-02-12");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Period_should_return_400_when_from_greater_than_to()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/periodo?merchantId=m1&from=2026-02-12&to=2026-02-10");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Daily_should_return_400_when_date_invalid_format()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/diario/bad-date?merchantId=m1");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip ="Ajustar")]
    public async Task Daily_should_return_200_and_zeros_when_no_data()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "balance-api",
            scopes: "balance.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/v1/consolidados/diario/2026-02-10?merchantId=m1");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"merchantId\":\"m1\"");
        body.Should().Contain("\"totalCredits\":\"0.00\"");
        body.Should().Contain("\"totalDebits\":\"0.00\"");
    }
}
