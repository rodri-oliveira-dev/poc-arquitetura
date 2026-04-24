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
        res.Headers.Location?.ToString().Should().Be($"/api/v1/lancamentos/{body.Id}");
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
        replayRes.Headers.Location.Should().Be(res.Headers.Location);

        var replayBody = await replayRes.Content.ReadFromJsonAsync<LancamentoDto>();
        replayBody.Should().NotBeNull();
        replayBody!.Should().BeEquivalentTo(body);
    }

    [Theory]
    [InlineData("desc changed", "ext")]
    [InlineData("desc", "ext changed")]
    public async Task Post_should_return_409_when_same_idempotency_key_is_reused_with_changed_description_or_external_reference(
        string description,
        string externalReference)
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: "ledger.write");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();

        using var firstReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new
            {
                merchantId = "m1",
                type = "CREDIT",
                amount = 10.0,
                description = "desc",
                externalReference = "ext"
            })
        };
        firstReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstRes = await _client.SendAsync(firstReq);
        firstRes.StatusCode.Should().Be(HttpStatusCode.Created);

        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new
            {
                merchantId = "m1",
                type = "CREDIT",
                amount = 10.0,
                description,
                externalReference
            })
        };
        replayReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var replayRes = await _client.SendAsync(replayReq);
        replayRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Options_preflight_should_allow_contract_headers()
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/lancamentos");
        req.Headers.Add("Origin", "http://localhost:5173");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "Idempotency-Key, X-Correlation-Id");

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        res.Headers.TryGetValues("Access-Control-Allow-Headers", out var values).Should().BeTrue();
        var allowedHeaders = string.Join(",", values!).ToLowerInvariant();
        allowedHeaders.Should().Contain("idempotency-key");
        allowedHeaders.Should().Contain("x-correlation-id");
    }
}
