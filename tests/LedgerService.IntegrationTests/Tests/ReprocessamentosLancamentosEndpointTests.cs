using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;
using LedgerService.Api.Contracts;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Tests;

public sealed class ReprocessamentosLancamentosEndpointTests : IClassFixture<LedgerApiFactory>
{
    private readonly LedgerApiFactory _factory;
    private readonly HttpClient _client;

    public ReprocessamentosLancamentosEndpointTests(LedgerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Post_reprocessar_should_return_202_and_location_for_valid_request()
    {
        Authenticate();
        var idempotencyKey = Guid.NewGuid().ToString();

        using var req = CreateRequest(idempotencyKey);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await res.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        body.Should().NotBeNull();
        body!.ReprocessamentoId.Should().NotBeEmpty();
        body.MerchantId.Should().Be("m1");
        body.DataInicial.Should().Be(new DateOnly(2026, 5, 1));
        body.DataFinal.Should().Be(new DateOnly(2026, 5, 6));
        body.Status.Should().Be("Pending");
        body.StatusUrl.Should().Be($"/api/v1/lancamentos/reprocessamentos/{body.ReprocessamentoId}");
        res.Headers.Location?.ToString().Should().Be(body.StatusUrl);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReprocessamentosLancamentos.Single(x => x.Id == body.ReprocessamentoId).Status
            .Should()
            .Be(ReprocessamentoLancamentosStatus.Pending);
        db.OutboxMessages.Single(x => x.AggregateId == body.ReprocessamentoId).EventType
            .Should()
            .Be("ReprocessamentoLancamentosSolicitado.v1");

        using var replayReq = CreateRequest(idempotencyKey);
        var replayRes = await _client.SendAsync(replayReq);

        replayRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        replayBody.Should().BeEquivalentTo(body);
        replayRes.Headers.Location?.ToString().Should().Be(body.StatusUrl);
    }

    [Fact]
    public async Task Post_reprocessar_should_return_400_for_invalid_period()
    {
        Authenticate();

        using var req = CreateRequest(
            Guid.NewGuid().ToString(),
            dataInicial: new DateOnly(2026, 5, 6),
            dataFinal: new DateOnly(2026, 5, 1));

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("dataFinal");
    }

    [Fact]
    public async Task Post_reprocessar_should_return_400_for_empty_motivo()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid().ToString(), motivo: "");

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("motivo");
    }

    [Fact]
    public async Task Post_reprocessar_should_return_400_when_period_exceeds_limit()
    {
        Authenticate();

        using var req = CreateRequest(
            Guid.NewGuid().ToString(),
            dataInicial: new DateOnly(2026, 5, 1),
            dataFinal: new DateOnly(2026, 6, 1));

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("31");
    }

    [Fact]
    public async Task Post_reprocessar_should_return_403_when_token_is_not_authorized_for_merchant()
    {
        Authenticate(merchantIds: "m2");

        using var req = CreateRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_reprocessar_should_return_401_without_token()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var req = CreateRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_200_for_existing_reprocessamento()
    {
        Authenticate(scopes: "ledger.write");

        using var createReq = CreateRequest(Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        created.Should().NotBeNull();

        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{created!.ReprocessamentoId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusReprocessamentoLancamentosResponse>();
        body.Should().NotBeNull();
        body!.ReprocessamentoId.Should().Be(created.ReprocessamentoId);
        body.Status.Should().Be("Pending");
        body.Motivo.Should().Be("Correcao de regra de consolidacao");
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_404_when_reprocessamento_does_not_exist()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_403_when_token_is_not_authorized_for_merchant()
    {
        Authenticate(scopes: "ledger.write", merchantIds: "m1");

        using var createReq = CreateRequest(Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        created.Should().NotBeNull();

        Authenticate(scopes: "ledger.read", merchantIds: "m2");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{created!.ReprocessamentoId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void Authenticate(string merchantIds = "m1", string scopes = "ledger.write")
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: scopes,
            merchantIds: merchantIds);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static HttpRequestMessage CreateRequest(
        string idempotencyKey,
        string merchantId = "m1",
        DateOnly? dataInicial = null,
        DateOnly? dataFinal = null,
        string motivo = "Correcao de regra de consolidacao")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos/reprocessar")
        {
            Content = JsonContent.Create(new
            {
                merchantId,
                dataInicial = dataInicial ?? new DateOnly(2026, 5, 1),
                dataFinal = dataFinal ?? new DateOnly(2026, 5, 6),
                motivo
            })
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
    }
}
