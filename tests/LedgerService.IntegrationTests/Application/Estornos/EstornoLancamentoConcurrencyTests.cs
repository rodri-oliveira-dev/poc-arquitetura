using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;
using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Application.Estornos;

[Collection(PostgresLedgerCollection.Name)]
public sealed class EstornoLancamentoConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresLedgerApiFactory _factory;

    public EstornoLancamentoConcurrencyTests(PostgresLedgerFixture fixture)
    {
        _factory = new PostgresLedgerApiFactory(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.CleanAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Post_estornos_concorrentes_should_create_only_one_active_request()
    {
        var lancamento = await SeedLancamentoAsync();
        using var client1 = CreateAuthenticatedClient();
        using var client2 = CreateAuthenticatedClient();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = SendAfterGateAsync(client1, lancamento.Id, Guid.NewGuid().ToString(), gate.Task);
        var second = SendAfterGateAsync(client2, lancamento.Id, Guid.NewGuid().ToString(), gate.Task);

        gate.SetResult();
        var responses = await Task.WhenAll(first, second);

        responses.Select(x => x.StatusCode).Should().BeEquivalentTo(
        [
            HttpStatusCode.Accepted,
            HttpStatusCode.Conflict
        ]);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estornos = await db.EstornosLancamentos
            .AsNoTracking()
            .Where(x => x.LancamentoOriginalId == lancamento.Id)
            .ToListAsync();

        estornos.Should().ContainSingle();
        estornos.Count(x => x.Status is EstornoLancamentoStatus.Pending or EstornoLancamentoStatus.Processing)
            .Should()
            .Be(1);

        var created = await responses.Single(x => x.StatusCode == HttpStatusCode.Accepted)
            .Content
            .ReadFromJsonAsync<SolicitarEstornoLancamentoResponse>();
        created.Should().NotBeNull();

        var requestOutboxCount = await db.OutboxMessages
            .Where(x => x.AggregateId == created!.EstornoId && x.EventType == LancamentoEstornoSolicitadoV1.EventType)
            .CountAsync();
        requestOutboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ClaimPendingAsync_concorrente_should_claim_estorno_once()
    {
        var lancamento = await SeedLancamentoAsync();
        var estorno = await SeedEstornoAsync(lancamento);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = ClaimAfterGateAsync(gate.Task);
        var second = ClaimAfterGateAsync(gate.Task);

        gate.SetResult();
        var claimed = await Task.WhenAll(first, second);

        claimed.Sum(x => x.Count).Should().Be(1);
        claimed.SelectMany(x => x).Should().ContainSingle(x => x.Id == estorno.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.EstornosLancamentos.AsNoTracking().SingleAsync(x => x.Id == estorno.Id);
        persisted.Status.Should().Be(EstornoLancamentoStatus.Processing);
    }

    [Fact]
    public async Task Processar_estorno_concorrente_should_create_only_one_compensating_entry()
    {
        var lancamento = await SeedLancamentoAsync();
        var estorno = await SeedEstornoAsync(lancamento);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = ProcessAfterGateAsync(estorno.Id, gate.Task);
        var second = ProcessAfterGateAsync(estorno.Id, gate.Task);

        gate.SetResult();
        await Task.WhenAll(first, second);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.EstornosLancamentos.AsNoTracking().SingleAsync(x => x.Id == estorno.Id);
        persisted.Status.Should().Be(EstornoLancamentoStatus.Completed);
        persisted.LancamentoCompensatorioId.Should().NotBeNull();

        var compensatingEntries = await db.LedgerEntries
            .Where(x => x.ExternalReference == $"estorno:{lancamento.Id:N}")
            .CountAsync();
        compensatingEntries.Should().Be(1);

        var finalOutboxCount = await db.OutboxMessages
            .Where(x => x.AggregateId == persisted.LancamentoCompensatorioId && x.EventType == LedgerEntryCreatedV1.EventType)
            .CountAsync();
        finalOutboxCount.Should().Be(1);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: "ledger.write",
            merchantIds: "m1");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<HttpResponseMessage> SendAfterGateAsync(
        HttpClient client,
        Guid lancamentoId,
        string idempotencyKey,
        Task gate)
    {
        await gate;
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/lancamentos/{lancamentoId}/estornos")
        {
            Content = JsonContent.Create(new { motivo = "Erro operacional no lancamento original" })
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(req);
    }

    private async Task<IReadOnlyList<EstornoLancamento>> ClaimAfterGateAsync(Task gate)
    {
        await gate;
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEstornoLancamentoRepository>();
        return await repo.ClaimPendingAsync(10);
    }

    private async Task ProcessAfterGateAsync(Guid estornoId, Task gate)
    {
        await gate;
        await using var scope = _factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new ProcessarEstornoLancamentoCommand(estornoId));
    }

    private async Task<LedgerEntry> SeedLancamentoAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lancamento = new LedgerEntry(
            "m1",
            LedgerEntryType.Credit,
            10m,
            DateTime.Now,
            "Venda",
            $"ext-{Guid.NewGuid():N}",
            Guid.NewGuid());

        await db.LedgerEntries.AddAsync(lancamento);
        await db.SaveChangesAsync();
        return lancamento;
    }

    private async Task<EstornoLancamento> SeedEstornoAsync(LedgerEntry lancamento)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estorno = new EstornoLancamento(
            lancamento.Id,
            lancamento.MerchantId,
            "Erro operacional no lancamento original",
            Guid.NewGuid());

        await db.EstornosLancamentos.AddAsync(estorno);
        await db.SaveChangesAsync();
        return estorno;
    }
}
