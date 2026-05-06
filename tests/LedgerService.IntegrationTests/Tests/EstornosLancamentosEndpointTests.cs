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

public sealed class EstornosLancamentosEndpointTests : IClassFixture<LedgerApiFactory>
{
    private readonly LedgerApiFactory _factory;
    private readonly HttpClient _client;

    public EstornosLancamentosEndpointTests(LedgerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Post_estornos_should_return_202_and_location_for_valid_request()
    {
        Authenticate();
        var lancamento = await SeedLancamentoAsync("m1");
        var idempotencyKey = Guid.NewGuid().ToString();

        using var req = CreateRequest(lancamento.Id, idempotencyKey);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await res.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        body.Should().NotBeNull();
        body!.EstornoId.Should().NotBeEmpty();
        body.LancamentoOriginalId.Should().Be(lancamento.Id);
        body.Status.Should().Be("Pending");
        body.StatusUrl.Should().Be($"/api/v1/lancamentos/estornos/{body.EstornoId}");
        res.Headers.Location?.ToString().Should().Be(body.StatusUrl);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EstornosLancamentos.Single(x => x.Id == body.EstornoId).Status.Should().Be(EstornoLancamentoStatus.Pending);
        db.OutboxMessages.Single(x => x.AggregateId == body.EstornoId).EventType.Should().Be("LancamentoEstornoSolicitado.v1");

        using var replayReq = CreateRequest(lancamento.Id, idempotencyKey);
        var replayRes = await _client.SendAsync(replayReq);

        replayRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        replayBody.Should().BeEquivalentTo(body);
        replayRes.Headers.Location?.ToString().Should().Be(body.StatusUrl);
    }

    [Fact]
    public async Task Post_estornos_should_return_400_for_invalid_payload()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid(), Guid.NewGuid().ToString(), "");

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("motivo");
    }

    [Fact]
    public async Task Post_estornos_should_return_404_when_lancamento_does_not_exist()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid(), Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_estornos_should_return_409_when_active_estorno_already_exists()
    {
        Authenticate();
        var lancamento = await SeedLancamentoAsync("m1");

        using var firstReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var firstRes = await _client.SendAsync(firstReq);
        firstRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var duplicateReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var duplicateRes = await _client.SendAsync(duplicateReq);

        duplicateRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_estornos_should_return_403_when_token_is_not_authorized_for_original_merchant()
    {
        Authenticate(merchantIds: "m2");
        var lancamento = await SeedLancamentoAsync("m1");

        using var req = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_estornos_should_return_200_for_existing_estorno()
    {
        Authenticate(scopes: "ledger.write");
        var lancamento = await SeedLancamentoAsync("m1");

        using var createReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var created = await createRes.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        created.Should().NotBeNull();

        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{created!.EstornoId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusEstornoLancamentoResponse>();
        body.Should().NotBeNull();
        body!.EstornoId.Should().Be(created.EstornoId);
        body.LancamentoOriginalId.Should().Be(lancamento.Id);
        body.Status.Should().Be("Pending");
        body.Motivo.Should().Be("Erro operacional no lancamento original");
        body.SolicitadoEm.Should().NotBe(default);
    }

    [Fact]
    public async Task Get_estornos_should_return_404_when_estorno_does_not_exist()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_estornos_should_return_404_for_invalid_estorno_id_route()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync("/api/v1/lancamentos/estornos/not-a-guid");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_estornos_should_return_403_when_token_is_not_authorized_for_estorno_merchant()
    {
        var estorno = await SeedEstornoAsync("m1");
        Authenticate(scopes: "ledger.read", merchantIds: "m2");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{estorno.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_estornos_should_return_401_without_token()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        Guid lancamentoId,
        string idempotencyKey,
        string motivo = "Erro operacional no lancamento original")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lancamentos/{lancamentoId}/estornos")
        {
            Content = JsonContent.Create(new { motivo })
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
    }

    private async Task<LedgerEntry> SeedLancamentoAsync(string merchantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lancamento = new LedgerEntry(
            merchantId,
            LedgerEntryType.Credit,
            10m,
            DateTime.Now,
            "desc",
            "ext",
            Guid.NewGuid());

        await db.LedgerEntries.AddAsync(lancamento);
        await db.SaveChangesAsync();

        return lancamento;
    }

    private async Task<EstornoLancamento> SeedEstornoAsync(string merchantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estorno = new EstornoLancamento(
            Guid.NewGuid(),
            merchantId,
            "Erro operacional no lancamento original",
            Guid.NewGuid());

        await db.EstornosLancamentos.AddAsync(estorno);
        await db.SaveChangesAsync();

        return estorno;
    }
}
