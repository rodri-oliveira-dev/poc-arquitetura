using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using LedgerService.Application.Common.Models;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;

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
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.0
        });
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
        var res = await _client.PostAsJsonAsync("/api/v1/lancamentos", new
        {
            merchantId = "m1",
            type = "CREDIT",
            amount = 10.0
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_should_create_lancamento_with_write_scope()
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "m1", type = "CREDIT", amount = 10.0 })
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);

        var res = await _client.SendAsync(req);

        // Contrato do endpoint: 201 + body (LancamentoDto) e header X-Correlation-Id
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        res.Headers.TryGetValues("X-Correlation-Id", out var correlationValues).Should().BeTrue();
        var correlationId = correlationValues!.Single();
        Guid.TryParse(correlationId, out _).Should().BeTrue("X-Correlation-Id deve ser um GUID válido");

        var body = await res.Content.ReadFromJsonAsync<LancamentoDto>();
        body.Should().NotBeNull();
        body!.Id.Should().StartWith("lan_");
        body.Id.Should().HaveLength("lan_".Length + 8);
        body.MerchantId.Should().Be("m1");
        body.Type.Should().Be("CREDIT");
        body.Amount.Should().Be("10.00");
        body.Description.Should().BeNull();
        body.ExternalReference.Should().BeNull();
        DateTimeOffset.TryParse(body.OccurredAt, out _).Should().BeTrue();
        DateTimeOffset.TryParse(body.CreatedAt, out _).Should().BeTrue();

        // Idempotência (cenário de sucesso): mesma Idempotency-Key + mesmo payload deve fazer replay da resposta.
        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "m1", type = "CREDIT", amount = 10.0 })
        };
        replayReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var replayRes = await _client.SendAsync(replayReq);
        replayRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var replayBody = await replayRes.Content.ReadFromJsonAsync<LancamentoDto>();
        replayBody.Should().NotBeNull();
        replayBody!.Should().BeEquivalentTo(body);
    }
}
