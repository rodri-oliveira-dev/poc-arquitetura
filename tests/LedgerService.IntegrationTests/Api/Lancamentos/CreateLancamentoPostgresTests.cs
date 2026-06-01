using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LedgerService.Application.Common.Models;
using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LedgerService.IntegrationTests.Api.Lancamentos;

[Collection(PostgresLedgerCollection.Name)]
public sealed class CreateLancamentoPostgresTests : IAsyncLifetime
{
    private readonly PostgresLedgerApiFactory _factory;
    private readonly HttpClient _client;

    public CreateLancamentoPostgresTests(PostgresLedgerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _factory = new PostgresLedgerApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.CleanAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Post_should_persist_entry_idempotency_and_outbox_once_when_response_is_replayed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.CreateToken());
        var idempotencyKey = Guid.NewGuid().ToString();

        using var request = CreateRequest(idempotencyKey);
        using var response = await _client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LancamentoDto>(cancellationToken);
        Assert.NotNull(body);

        using var replayRequest = CreateRequest(idempotencyKey);
        using var replayResponse = await _client.SendAsync(replayRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var replayBody = await replayResponse.Content.ReadFromJsonAsync<LancamentoDto>(cancellationToken);
        Assert.Equal(body, replayBody);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LedgerEntries.CountAsync(cancellationToken));
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync(cancellationToken));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Post_should_return_conflict_without_new_rows_when_idempotency_key_is_reused_with_different_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.CreateToken());
        var idempotencyKey = Guid.NewGuid().ToString();

        using var request = CreateRequest(idempotencyKey);
        using var response = await _client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var conflictingRequest = CreateRequest(idempotencyKey, amount: 20.00m);
        using var conflictingResponse = await _client.SendAsync(conflictingRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflictingResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LedgerEntries.CountAsync(cancellationToken));
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync(cancellationToken));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Database_should_enforce_unique_idempotency_key_per_merchant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var idempotencyKey = Guid.NewGuid().ToString();

        db.IdempotencyRecords.Add(CreateIdempotencyRecord("tese", idempotencyKey, now));
        await db.SaveChangesAsync(cancellationToken);

        db.IdempotencyRecords.Add(CreateIdempotencyRecord("tese", idempotencyKey, now));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(cancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_idempotency_records_merchant_key", postgresException.ConstraintName);
    }

    private static HttpRequestMessage CreateRequest(string idempotencyKey, decimal amount = 10.00m)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "tese", type = "CREDIT", amount })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private static IdempotencyRecord CreateIdempotencyRecord(
        string merchantId,
        string idempotencyKey,
        DateTime now)
        => new(
            merchantId,
            idempotencyKey,
            requestHash: Guid.NewGuid().ToString("N"),
            ledgerEntryId: null,
            responseStatusCode: 201,
            responseBody: null,
            createdAt: now,
            expiresAt: now.AddDays(7));
}
