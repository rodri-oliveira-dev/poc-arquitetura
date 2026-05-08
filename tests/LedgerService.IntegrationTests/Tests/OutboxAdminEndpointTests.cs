using FluentAssertions;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LedgerService.IntegrationTests.Tests;

public sealed class OutboxAdminEndpointTests : IClassFixture<LedgerApiFactory>
{
    private readonly HttpClient _client;
    private readonly LedgerApiFactory _factory;

    public OutboxAdminEndpointTests(LedgerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task RequeueFailed_should_require_outbox_requeue_scope()
    {
        Authenticate("ledger.write");

        var res = await _client.PostAsJsonAsync("/api/v1/outbox/failed/requeue", new
        {
            outboxMessageId = Guid.NewGuid(),
            reason = "broker recuperado"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequeueFailed_should_requeue_failed_message()
    {
        Authenticate("ledger.outbox.requeue");
        var outboxId = await SeedFailedOutboxMessageAsync();

        var res = await _client.PostAsJsonAsync("/api/v1/outbox/failed/requeue", new
        {
            outboxMessageId = outboxId,
            reason = "broker recuperado"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = db.OutboxMessages.Single(x => x.Id == outboxId);

        refreshed.Status.Should().Be(OutboxStatus.Pending);
        refreshed.Attempts.Should().Be(0);
        refreshed.RequeueCount.Should().Be(1);
        refreshed.LastRequeuedBy.Should().Be("poc-usuario");
        refreshed.LastRequeueReason.Should().Be("broker recuperado");
    }

    private void Authenticate(string scopes)
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: "https://auth-api",
            audiences: "ledger-api",
            scopes: scopes);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Guid> SeedFailedOutboxMessageAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var message = new OutboxMessage(
            aggregateType: "LedgerEntry",
            aggregateId: Guid.NewGuid(),
            eventType: "LedgerEntryCreated.v1",
            payload: "{}",
            occurredAt: DateTime.Now.AddMinutes(-1),
            correlationId: Guid.NewGuid());

        message.MarkFailedAttempt(1, DateTime.Now.AddSeconds(10), "kafka down");

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        return message.Id;
    }
}
