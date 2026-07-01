using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Domain.FunctionalAuditing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditService.Api.Tests;

public sealed class AuditRecordsEndpointTests
{
    [Fact]
    public async Task Post_valid_payload_should_return_201_created()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = CreatePost(ValidPayload(), "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CreateAuditRecordResponse? body = await response.Content.ReadFromJsonAsync<CreateAuditRecordResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task Post_valid_payload_should_persist_record()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = CreatePost(ValidPayload(), "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        CreateAuditRecordResponse body = (await response.Content.ReadFromJsonAsync<CreateAuditRecordResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;

        FunctionalAuditRecord record = Assert.Single(factory.Store.Records);

        Assert.Equal(body.Id, record.Id);
        Assert.Equal("LedgerService", record.SourceService);
        Assert.Equal("LancamentoCriado", record.OperationType);
        Assert.Equal("lan_123", record.EntityId);
        Assert.Equal("100.00", record.Metadata["amount"]);
    }

    [Fact]
    public async Task Post_without_idempotency_key_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = CreatePost(ValidPayload(), idempotencyKey: null);

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_invalid_idempotency_key_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = CreatePost(ValidPayload(), "not-a-guid");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_repeated_with_same_key_and_payload_should_return_same_result()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        using var firstRequest = CreatePost(ValidPayload(), "00000000-0000-0000-0000-000000000003");
        using var secondRequest = CreatePost(ValidPayload(), "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage firstResponse = await client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        HttpResponseMessage secondResponse = await client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        CreateAuditRecordResponse first = (await firstResponse.Content.ReadFromJsonAsync<CreateAuditRecordResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;
        CreateAuditRecordResponse second = (await secondResponse.Content.ReadFromJsonAsync<CreateAuditRecordResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(first.Id, second.Id);

        Assert.Single(factory.Store.Records);
    }

    [Fact]
    public async Task Post_repeated_with_same_key_and_different_payload_should_return_409()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();
        var changedPayload = ValidPayload() with
        {
            OperationType = "LancamentoEstornado"
        };

        using var firstRequest = CreatePost(ValidPayload(), "00000000-0000-0000-0000-000000000003");
        using var secondRequest = CreatePost(changedPayload, "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage firstResponse = await client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        HttpResponseMessage secondResponse = await client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Post_invalid_payload_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();
        var invalidPayload = ValidPayload() with
        {
            OperationId = Guid.Empty
        };

        using var request = CreatePost(invalidPayload, "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_metadata_above_limit_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();
        var payload = ValidPayload() with
        {
            Metadata = new Dictionary<string, string>
            {
                ["oversized"] = new('a', FunctionalAuditRecord.MetadataMaxBytes)
            }
        };

        using var request = CreatePost(payload, "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_should_keep_contract_agnostic_to_specific_services()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();
        var payload = ValidPayload() with
        {
            SourceService = "AnyCallingService",
            OperationType = "AnyBusinessOperationCompleted",
            EntityType = "AnyEntity",
            EntityId = "any-123"
        };

        using var request = CreatePost(payload, "00000000-0000-0000-0000-000000000003");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        FunctionalAuditRecord record = Assert.Single(factory.Store.Records);
        Assert.Equal("AnyCallingService", record.SourceService);
        Assert.Equal("AnyBusinessOperationCompleted", record.OperationType);
    }

    private static HttpRequestMessage CreatePost(CreateAuditRecordRequest payload, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audit-records")
        {
            Content = JsonContent.Create(payload)
        };

        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }

    private static CreateAuditRecordRequest ValidPayload()
        => new(
            OperationId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CorrelationId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            SourceService: "LedgerService",
            OperationType: "LancamentoCriado",
            EntityType: "Lancamento",
            EntityId: "lan_123",
            MerchantId: "m1",
            Actor: new CreateAuditRecordActorRequest("Client", "poc-automation", "poc-automation"),
            Status: "Succeeded",
            Reason: null,
            Metadata: new Dictionary<string, string>
            {
                ["amount"] = "100.00",
                ["currency"] = "BRL"
            },
            OccurredAt: DateTimeOffset.Parse("2026-06-30T10:30:00Z", CultureInfo.InvariantCulture));

    private sealed class AuditApiFactory : WebApplicationFactory<Program>
    {
        public AuditRecordStore Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFunctionalAuditRecordRepository>();
                services.AddSingleton(Store);
                services.AddScoped<IFunctionalAuditRecordRepository, FakeFunctionalAuditRecordRepository>();
            });
        }
    }

    private sealed class AuditRecordStore
    {
        private readonly List<FunctionalAuditRecord> _records = [];

        public IReadOnlyCollection<FunctionalAuditRecord> Records => _records;

        public FunctionalAuditRecord? GetByIdempotencyKey(string idempotencyKey)
            => _records.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);

        public void Add(FunctionalAuditRecord record)
            => _records.Add(record);
    }

    private sealed class FakeFunctionalAuditRecordRepository(AuditRecordStore store)
        : IFunctionalAuditRecordRepository
    {
        public Task<FunctionalAuditRecord?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(store.GetByIdempotencyKey(idempotencyKey));

        public Task AddAsync(FunctionalAuditRecord record, CancellationToken cancellationToken = default)
        {
            store.Add(record);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record CreateAuditRecordRequest(
        Guid OperationId,
        Guid? CorrelationId,
        string SourceService,
        string OperationType,
        string? EntityType,
        string? EntityId,
        string? MerchantId,
        CreateAuditRecordActorRequest? Actor,
        string Status,
        string? Reason,
        Dictionary<string, string>? Metadata,
        DateTimeOffset OccurredAt);

    private sealed record CreateAuditRecordActorRequest(
        string? Type,
        string? Subject,
        string? ClientId);

    private sealed record CreateAuditRecordResponse(Guid Id);
}
