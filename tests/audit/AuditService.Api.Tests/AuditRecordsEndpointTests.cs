using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;
using AuditService.Domain.FunctionalAuditing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ApplicationDependencyInjection = AuditService.Application.DependencyInjection;
using InfrastructureDependencyInjection = AuditService.Infrastructure.DependencyInjection;

namespace AuditService.Api.Tests;

public sealed class AuditRecordsEndpointTests
{
    private static readonly DateTimeOffset BaseInstant =
        DateTimeOffset.Parse("2026-06-30T10:30:00Z", CultureInfo.InvariantCulture);

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

    [Fact]
    public async Task Get_by_id_should_return_existing_record()
    {
        using var factory = new AuditApiFactory();
        FunctionalAuditRecord record = factory.Store.Add(CreateRecord());
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-records/{record.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditRecordResponse body = (await response.Content.ReadFromJsonAsync<AuditRecordResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;
        Assert.Equal(record.Id, body.Id);
        Assert.Equal("100.00", body.Metadata["amount"]);
    }

    [Fact]
    public async Task Get_by_id_should_return_404_when_record_does_not_exist()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-records/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_operation_id_should_return_functional_trail_ordered_by_occurred_at_ascending()
    {
        using var factory = new AuditApiFactory();
        const string operationId = "00000000-0000-0000-0000-000000000111";
        factory.Store.Add(CreateRecord(operationId: operationId, occurredAt: BaseInstant.AddMinutes(2), status: "Succeeded"));
        factory.Store.Add(CreateRecord(operationId: operationId, occurredAt: BaseInstant, status: "Received"));
        factory.Store.Add(CreateRecord(operationId: "00000000-0000-0000-0000-000000000222", occurredAt: BaseInstant.AddMinutes(1)));
        factory.Store.Add(CreateRecord(operationId: operationId, occurredAt: BaseInstant.AddMinutes(1), status: "Rejected"));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-records/operations/{operationId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditRecordResponse[] body = (await response.Content.ReadFromJsonAsync<AuditRecordResponse[]>(
            cancellationToken: TestContext.Current.CancellationToken))!;
        Assert.Equal(["Received", "Rejected", "Succeeded"], [.. body.Select(x => x.Status)]);
        Assert.All(body, item => Assert.Equal(operationId, item.OperationId));
    }

    [Fact]
    public async Task Search_should_filter_by_merchant_id()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(merchantId: "merchant-a"));
        factory.Store.Add(CreateRecord(merchantId: "merchant-b"));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "merchantId=merchant-a");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("merchant-a", item.MerchantId);
    }

    [Fact]
    public async Task Search_should_filter_by_source_service()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(sourceService: "AuditProducerA"));
        factory.Store.Add(CreateRecord(sourceService: "AuditProducerB"));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "sourceService=AuditProducerA");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("AuditProducerA", item.SourceService);
    }

    [Fact]
    public async Task Search_should_filter_by_operation_type()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(operationType: "OperationApproved"));
        factory.Store.Add(CreateRecord(operationType: "OperationRejected"));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "operationType=OperationApproved");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("OperationApproved", item.OperationType);
    }

    [Fact]
    public async Task Search_should_filter_by_status()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(status: "Succeeded"));
        factory.Store.Add(CreateRecord(status: "Failed"));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "status=Failed");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("Failed", item.Status);
    }

    [Fact]
    public async Task Search_should_filter_by_entity_type_and_entity_id()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(entityType: "Transfer", entityId: "trf-1"));
        factory.Store.Add(CreateRecord(entityType: "Transfer", entityId: "trf-2"));
        factory.Store.Add(CreateRecord(entityType: "LedgerEntry", entityId: "trf-1"));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "entityType=Transfer&entityId=trf-1");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("Transfer", item.EntityType);
        Assert.Equal("trf-1", item.EntityId);
    }

    [Fact]
    public async Task Search_without_required_period_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/audit-records?merchantId=m1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_with_page_size_above_limit_should_return_400()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-records?{PeriodQuery()}&pageSize=101",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_should_order_by_occurred_at_descending()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(operationType: "Oldest", occurredAt: BaseInstant));
        factory.Store.Add(CreateRecord(operationType: "Newest", occurredAt: BaseInstant.AddMinutes(2)));
        factory.Store.Add(CreateRecord(operationType: "Middle", occurredAt: BaseInstant.AddMinutes(1)));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, string.Empty);

        Assert.Equal(["Newest", "Middle", "Oldest"], [.. body.Items.Select(x => x.OperationType)]);
    }

    [Fact]
    public async Task Search_should_return_requested_page()
    {
        using var factory = new AuditApiFactory();
        factory.Store.Add(CreateRecord(operationType: "Third", occurredAt: BaseInstant));
        factory.Store.Add(CreateRecord(operationType: "Second", occurredAt: BaseInstant.AddMinutes(1)));
        factory.Store.Add(CreateRecord(operationType: "First", occurredAt: BaseInstant.AddMinutes(2)));
        using HttpClient client = factory.CreateClient();

        PagedResponse<AuditRecordResponse> body = await SearchAsync(client, "page=2&pageSize=1");

        AuditRecordResponse item = Assert.Single(body.Items);
        Assert.Equal("Second", item.OperationType);
        Assert.Equal(2, body.Page);
        Assert.Equal(1, body.PageSize);
        Assert.Equal(3, body.TotalItems);
        Assert.Equal(3, body.TotalPages);
    }

    [Fact]
    public void AuditService_should_not_reference_other_financial_bounded_contexts()
    {
        string[] forbiddenAssemblyPrefixes = ["LedgerService", "BalanceService", "TransferService"];
        var auditAssemblies = new[]
        {
            typeof(Program).Assembly,
            typeof(ApplicationDependencyInjection).Assembly,
            typeof(FunctionalAuditRecord).Assembly,
            typeof(InfrastructureDependencyInjection).Assembly
        };

        var references = auditAssemblies
            .SelectMany(static assembly => assembly.GetReferencedAssemblies())
            .Select(static reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            forbiddenAssemblyPrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
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

    private static async Task<PagedResponse<AuditRecordResponse>> SearchAsync(HttpClient client, string extraQuery)
    {
        string query = PeriodQuery();
        if (!string.IsNullOrWhiteSpace(extraQuery))
            query = $"{query}&{extraQuery}";

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-records?{query}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<AuditRecordResponse>>(
            cancellationToken: TestContext.Current.CancellationToken))!;
    }

    private static string PeriodQuery()
        => "from=2026-06-30T00%3A00%3A00Z&to=2026-07-01T00%3A00%3A00Z";

    private static FunctionalAuditRecord CreateRecord(
        string operationId = "00000000-0000-0000-0000-000000000001",
        string sourceService = "AuditProducer",
        string operationType = "OperationCompleted",
        string status = "Succeeded",
        DateTimeOffset? occurredAt = null,
        string? entityType = "FunctionalOperation",
        string? entityId = "op-1",
        string? merchantId = "m1")
        => FunctionalAuditRecord.Create(
            operationId: operationId,
            sourceService: sourceService,
            operationType: operationType,
            status: status,
            occurredAt: occurredAt ?? BaseInstant,
            correlationId: "00000000-0000-0000-0000-000000000002",
            idempotencyKey: Guid.NewGuid().ToString(),
            entityType: entityType,
            entityId: entityId,
            merchantId: merchantId,
            actorType: "Client",
            actorSubject: "poc-automation",
            actorClientId: "poc-automation",
            metadata: new Dictionary<string, string>
            {
                ["amount"] = "100.00",
                ["currency"] = "BRL"
            },
            createdAt: occurredAt ?? BaseInstant);

    private sealed class AuditApiFactory : WebApplicationFactory<Program>
    {
        public AuditRecordStore Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFunctionalAuditRecordRepository>();
                services.RemoveAll<IFunctionalAuditRecordQueryService>();
                services.AddSingleton(Store);
                services.AddScoped<IFunctionalAuditRecordRepository, FakeFunctionalAuditRecordRepository>();
                services.AddScoped<IFunctionalAuditRecordQueryService, FakeFunctionalAuditRecordQueryService>();
            });
        }
    }

    private sealed class AuditRecordStore
    {
        private readonly List<FunctionalAuditRecord> _records = [];

        public IReadOnlyCollection<FunctionalAuditRecord> Records => _records;

        public FunctionalAuditRecord? GetByIdempotencyKey(string idempotencyKey)
            => _records.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);

        public FunctionalAuditRecord Add(FunctionalAuditRecord record)
        {
            _records.Add(record);
            return record;
        }
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

    private sealed class FakeFunctionalAuditRecordQueryService(AuditRecordStore store)
        : IFunctionalAuditRecordQueryService
    {
        public Task<AuditRecordReadModel?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(store.Records.SingleOrDefault(x => x.Id == id) is { } record
                ? ToReadModel(record)
                : null);

        public Task<IReadOnlyCollection<AuditRecordReadModel>> GetByOperationIdAsync(
            string operationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<AuditRecordReadModel>>(
                [.. store.Records
                    .Where(x => x.OperationId == operationId)
                    .OrderBy(x => x.OccurredAt)
                    .ThenBy(x => x.CreatedAt)
                    .Select(ToReadModel)]);

        public Task<PagedResult<AuditRecordReadModel>> SearchAsync(
            AuditRecordSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<FunctionalAuditRecord> query = store.Records;

            if (!string.IsNullOrWhiteSpace(criteria.MerchantId))
                query = query.Where(x => x.MerchantId == criteria.MerchantId);

            if (!string.IsNullOrWhiteSpace(criteria.SourceService))
                query = query.Where(x => x.SourceService == criteria.SourceService);

            if (!string.IsNullOrWhiteSpace(criteria.OperationType))
                query = query.Where(x => x.OperationType == criteria.OperationType);

            if (!string.IsNullOrWhiteSpace(criteria.Status))
                query = query.Where(x => x.Status == criteria.Status);

            if (!string.IsNullOrWhiteSpace(criteria.EntityType))
                query = query.Where(x => x.EntityType == criteria.EntityType);

            if (!string.IsNullOrWhiteSpace(criteria.EntityId))
                query = query.Where(x => x.EntityId == criteria.EntityId);

            query = query.Where(x => x.OccurredAt >= criteria.From && x.OccurredAt <= criteria.To);

            FunctionalAuditRecord[] ordered =
            [
                .. query
                .OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.CreatedAt)
            ];

            int totalItems = ordered.Length;
            int totalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)criteria.PageSize);

            var pageItems = ordered
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .Select(ToReadModel);

            return Task.FromResult(new PagedResult<AuditRecordReadModel>(
                [.. pageItems],
                criteria.Page,
                criteria.PageSize,
                totalItems,
                totalPages));
        }

        private static AuditRecordReadModel ToReadModel(FunctionalAuditRecord record)
            => new(
                record.Id,
                record.OperationId,
                record.CorrelationId,
                record.SourceService,
                record.OperationType,
                record.EntityType,
                record.EntityId,
                record.MerchantId,
                record.ActorType is null && record.ActorSubject is null && record.ActorClientId is null
                    ? null
                    : new AuditRecordActorReadModel(record.ActorType, record.ActorSubject, record.ActorClientId),
                record.Status,
                record.Reason,
                record.Metadata,
                record.OccurredAt,
                record.CreatedAt);
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

    private sealed record AuditRecordResponse(
        Guid Id,
        string OperationId,
        string? CorrelationId,
        string SourceService,
        string OperationType,
        string? EntityType,
        string? EntityId,
        string? MerchantId,
        AuditRecordActorResponse? Actor,
        string Status,
        string? Reason,
        IReadOnlyDictionary<string, string> Metadata,
        DateTimeOffset OccurredAt,
        DateTimeOffset CreatedAt);

    private sealed record AuditRecordActorResponse(
        string? Type,
        string? Subject,
        string? ClientId);

    private sealed record PagedResponse<T>(
        IReadOnlyCollection<T> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);
}
