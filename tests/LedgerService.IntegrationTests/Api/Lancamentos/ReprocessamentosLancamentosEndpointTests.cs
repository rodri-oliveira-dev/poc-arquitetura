using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Api.Lancamentos;

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
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.ReprocessamentoId);
        Assert.Equal("m1", body.MerchantId);
        Assert.Equal(new DateOnly(2026, 5, 1), body.DataInicial);
        Assert.Equal(new DateOnly(2026, 5, 6), body.DataFinal);
        Assert.Equal("Pending", body.Status);
        Assert.Equal($"/api/v1/lancamentos/reprocessamentos/{body.ReprocessamentoId}", body.StatusUrl);
        Assert.Equal(body.StatusUrl, res.Headers.Location?.ToString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(
            ReprocessamentoLancamentosStatus.Pending,
            db.ReprocessamentosLancamentos.Single(x => x.Id == body.ReprocessamentoId).Status);
        Assert.Equal(
            "ReprocessamentoLancamentosSolicitado.v1",
            db.OutboxMessages.Single(x => x.AggregateId == body.ReprocessamentoId).EventType);

        using var replayReq = CreateRequest(idempotencyKey);
        var replayRes = await _client.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.Accepted, replayRes.StatusCode);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        Assert.Equivalent(body, replayBody);
        Assert.Equal(body.StatusUrl, replayRes.Headers.Location?.ToString());
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
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.Contains("dataFinal", raw);
    }

    [Fact]
    public async Task Post_reprocessar_should_return_400_for_empty_motivo()
    {
        Authenticate();

        using var req = CreateRequest(Guid.NewGuid().ToString(), motivo: "");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.Contains("motivo", raw);
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
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.Contains("31", raw);
    }

    [Fact]
    public async Task Post_reprocessar_should_return_403_when_token_is_not_authorized_for_merchant()
    {
        Authenticate(merchantIds: "m2");

        using var req = CreateRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_reprocessar_should_return_401_without_token()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var req = CreateRequest(Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_200_for_existing_reprocessamento()
    {
        Authenticate(scopes: "ledger.write");

        using var createReq = CreateRequest(Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Accepted, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        Assert.NotNull(created);
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{created!.ReprocessamentoId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ObterStatusReprocessamentoLancamentosResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.ReprocessamentoId, body!.ReprocessamentoId);
        Assert.Equal("Pending", body.Status);
        Assert.Equal("Correcao de regra de consolidacao", body.Motivo);
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_404_when_reprocessamento_does_not_exist()
    {
        Authenticate(scopes: "ledger.read");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_reprocessamentos_should_return_403_when_token_is_not_authorized_for_merchant()
    {
        Authenticate(scopes: "ledger.write", merchantIds: "m1");

        using var createReq = CreateRequest(Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Accepted, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        Assert.NotNull(created);
        Authenticate(scopes: "ledger.read", merchantIds: "m2");

        var res = await _client.GetAsync($"/api/v1/lancamentos/reprocessamentos/{created!.ReprocessamentoId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Processar_reprocessamento_should_republish_only_eligible_entries_and_complete_job()
    {
        Authenticate();
        var eligible = await SeedLancamentoAsync("m1", new DateTime(2026, 5, 2, 10, 0, 0), LedgerEntryType.Credit, 10m);
        await SeedLancamentoAsync("m1", new DateTime(2026, 5, 8, 10, 0, 0), LedgerEntryType.Credit, 20m);
        await SeedLancamentoAsync("m2", new DateTime(2026, 5, 2, 10, 0, 0), LedgerEntryType.Credit, 30m);

        using var createReq = CreateRequest(Guid.NewGuid().ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Accepted, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SolicitarReprocessamentoLancamentosResponse>();
        Assert.NotNull(created);
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ProcessarReprocessamentoLancamentosCommand(created!.ReprocessamentoId));
            await sender.Send(new ProcessarReprocessamentoLancamentosCommand(created.ReprocessamentoId));
        }

        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reprocessamento = db.ReprocessamentosLancamentos.Single(x => x.Id == created!.ReprocessamentoId);
        Assert.Equal(ReprocessamentoLancamentosStatus.Completed, reprocessamento.Status);
        Assert.NotNull(reprocessamento.ProcessingStartedAt);
        Assert.NotNull(reprocessamento.CompletedAt);
        var outboxMessage = Assert.Single(db.OutboxMessages.Where(x =>
            x.AggregateType == "LedgerEntryReprocessamento" &&
            x.EventType == "LedgerEntryCreated.v1"));
        Assert.Equal(eligible.Id, outboxMessage.AggregateId);
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

    private async Task<LedgerEntry> SeedLancamentoAsync(
        string merchantId,
        DateTime occurredAt,
        LedgerEntryType type,
        decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lancamento = new LedgerEntry(
            merchantId,
            type,
            amount,
            occurredAt,
            "desc",
            null,
            Guid.NewGuid());

        await db.LedgerEntries.AddAsync(lancamento);
        await db.SaveChangesAsync();

        return lancamento;
    }
}
