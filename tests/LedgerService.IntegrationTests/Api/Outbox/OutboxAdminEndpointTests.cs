using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LedgerService.IntegrationTests.Api.Outbox;

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
    public async Task RequeueDeadLetter_should_require_outbox_admin_scope()
    {
        Authenticate("ledger.write");

        var res = await _client.PostAsJsonAsync($"/api/v1/outbox/dead-letters/{Guid.NewGuid()}/requeue", new
        {
            reason = "broker recuperado"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RequeueDeadLetter_should_requeue_dead_letter_message()
    {
        Authenticate("outbox.admin");
        var outboxId = await SeedDeadLetterOutboxMessageAsync();

        var res = await _client.PostAsJsonAsync($"/api/v1/outbox/dead-letters/{outboxId}/requeue", new
        {
            reason = "broker recuperado"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshed = db.OutboxMessages.Single(x => x.Id == outboxId);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.Equal(0, refreshed.RetryCount);
        Assert.Equal(1, refreshed.RequeueCount);
        Assert.Equal("poc-usuario", refreshed.LastRequeuedBy);
        Assert.Equal("broker recuperado", refreshed.LastRequeueReason);
        Assert.Null(refreshed.LastError);
    }

    [Fact]
    public async Task GetDeadLetters_should_return_paginated_messages()
    {
        Authenticate("outbox.admin");
        var outboxId = await SeedDeadLetterOutboxMessageAsync();

        var res = await _client.GetAsync("/api/v1/outbox/dead-letters?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(outboxId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kafka down", content, StringComparison.OrdinalIgnoreCase);
    }

    private void Authenticate(string scopes)
    {
        var token = TestJwtTokenFactory.CreateToken(
            issuer: TestJwtTokenFactory.KeycloakIssuer,
            audiences: "ledger-api",
            scopes: scopes);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Guid> SeedDeadLetterOutboxMessageAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var message = new OutboxMessage(
            aggregateType: "LedgerEntry",
            aggregateId: Guid.NewGuid(),
            eventType: "LedgerEntryCreated.v1",
            payload: "{}",
            occurredAt: DateTime.UtcNow.AddMinutes(-1),
            correlationId: Guid.NewGuid());

        message.MarkFailedPublishAttempt(1, DateTime.UtcNow.AddSeconds(10), "kafka down");

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return message.Id;
    }
}
