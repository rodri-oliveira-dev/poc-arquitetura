using FluentAssertions;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LedgerService.IntegrationTests.Tests;

public sealed class LancamentosAuthorizationTests : IClassFixture<LedgerApiFactory>
{
    private readonly HttpClient _client;

    public LancamentosAuthorizationTests(LedgerApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_should_return_401_without_token()
    {
        // LedgerService não expõe GET /lancamentos; o endpoint protegido é POST.
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new { merchantId = "m1", type = "CREDIT", amount = 10.0 });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_should_return_403_when_missing_write_scope()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: "ledger.read");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Mesmo com token válido, sem o scope ledger.write, deve retornar 403.
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new { merchantId = "m1", type = "CREDIT", amount = 10.0 });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
