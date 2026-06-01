using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using LedgerService.Application.Common.Models;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure;
using LedgerService.IntegrationTests.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    private static HttpRequestMessage CreateRequest(string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new { merchantId = "tese", type = "CREDIT", amount = 10.00m })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }
}
