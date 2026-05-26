using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Api.Lancamentos;

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
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.EstornoId);
        Assert.Equal(lancamento.Id, body.LancamentoOriginalId);
        Assert.Equal("Pending", body.Status);
        Assert.Equal($"/api/v1/lancamentos/estornos/{body.EstornoId}", body.StatusUrl);
        Assert.Equal(body.StatusUrl, res.Headers.Location?.ToString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(EstornoLancamentoStatus.Pending, db.EstornosLancamentos.Single(x => x.Id == body.EstornoId).Status);
        Assert.Equal("LancamentoEstornoSolicitado.v1", db.OutboxMessages.Single(x => x.AggregateId == body.EstornoId).EventType);
        using var replayReq = CreateRequest(lancamento.Id, idempotencyKey);
        var replayRes = await _client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Accepted, replayRes.StatusCode);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        Assert.Equivalent(body, replayBody);
        Assert.Equal(body.StatusUrl, replayRes.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_estornos_should_return_400_for_invalid_payload()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid(), Guid.NewGuid().ToString(), "");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.Contains("motivo", raw);
    }

    [Fact]
    public async Task Post_estornos_should_return_404_when_lancamento_does_not_exist()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid(), Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Post_estornos_should_return_409_when_active_estorno_already_exists()
    {
        Authenticate();
        var lancamento = await SeedLancamentoAsync("m1");

        using var firstReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var firstRes = await _client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.Accepted, firstRes.StatusCode);
        using var duplicateReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var duplicateRes = await _client.SendAsync(duplicateReq);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRes.StatusCode);
    }

    [Fact]
    public async Task Post_estornos_should_return_403_when_token_is_not_authorized_for_original_merchant()
    {
        Authenticate(merchantIds: "m2");
        var lancamento = await SeedLancamentoAsync("m1");

        using var req = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_estornos_should_return_403_when_token_has_no_merchant_claim()
    {
        Authenticate(merchantIds: null);
        var lancamento = await SeedLancamentoAsync("m1");

        using var req = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Processar_estorno_should_persist_compensating_lancamento_and_final_outbox_event()
    {
        Authenticate();
        var lancamento = await SeedLancamentoAsync("m1");

        using var createReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Accepted, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        Assert.NotNull(created);
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ProcessarEstornoLancamentoCommand(created!.EstornoId));
            await sender.Send(new ProcessarEstornoLancamentoCommand(created.EstornoId));
        }

        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estorno = db.EstornosLancamentos.Single(x => x.Id == created!.EstornoId);
        Assert.Equal(EstornoLancamentoStatus.Completed, estorno.Status);
        Assert.NotNull(estorno.LancamentoCompensatorioId);
        var compensating = db.LedgerEntries.Single(x => x.Id == estorno.LancamentoCompensatorioId);
        Assert.Equal(LedgerEntryType.Debit, compensating.Type);
        Assert.Equal(-10m, compensating.Amount);
        Assert.Equal($"estorno:{lancamento.Id:N}", compensating.ExternalReference);
        Assert.Single(db.LedgerEntries.Where(x => x.ExternalReference == $"estorno:{lancamento.Id:N}"));

        Assert.Single(db.OutboxMessages.Where(x => x.AggregateId == compensating.Id && x.EventType == LedgerEntryCreatedV1.EventType));
    }

    [Fact]
    public async Task Get_estornos_should_return_200_for_existing_estorno()
    {
        Authenticate(scopes: "ledger.write");
        var lancamento = await SeedLancamentoAsync("m1");

        using var createReq = CreateRequest(lancamento.Id, Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Accepted, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        Assert.NotNull(created);
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{created!.EstornoId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusEstornoLancamentoResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.EstornoId, body!.EstornoId);
        Assert.Equal(lancamento.Id, body.LancamentoOriginalId);
        Assert.Equal("Pending", body.Status);
        Assert.Equal("Erro operacional no lancamento original", body.Motivo);
        Assert.NotEqual(default, body.SolicitadoEm);
    }

    [Fact]
    public async Task Get_estornos_should_return_404_when_estorno_does_not_exist()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_estornos_should_return_404_for_invalid_estorno_id_route()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync("/api/v1/lancamentos/estornos/not-a-guid");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_estornos_should_return_403_when_token_is_not_authorized_for_estorno_merchant()
    {
        var estorno = await SeedEstornoAsync("m1");
        Authenticate(scopes: "ledger.read", merchantIds: "m2");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{estorno.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Get_estornos_should_return_403_without_ledger_read_scope()
    {
        var estorno = await SeedEstornoAsync("m1");
        Authenticate(scopes: "ledger.write");

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{estorno.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Get_estornos_should_return_403_when_token_has_no_merchant_claim()
    {
        var estorno = await SeedEstornoAsync("m1");
        Authenticate(scopes: "ledger.read", merchantIds: null);

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{estorno.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Get_estornos_should_return_401_without_token()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var res = await _client.GetAsync($"/api/v1/lancamentos/estornos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private void Authenticate(string? merchantIds = "m1", string scopes = "ledger.write")
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
