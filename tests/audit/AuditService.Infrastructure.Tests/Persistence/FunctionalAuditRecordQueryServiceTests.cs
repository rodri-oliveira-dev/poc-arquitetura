using System.Globalization;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.FunctionalAuditing.ReadModels;
using AuditService.Domain.FunctionalAuditing;
using AuditService.Infrastructure.Persistence;
using AuditService.Infrastructure.Persistence.Queries;
using AuditService.Infrastructure.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Tests.Persistence;

[Collection(AuditPostgresCollection.Name)]
public sealed class FunctionalAuditRecordQueryServiceTests(PostgresAuditFixture fixture)
{
    private static readonly DateTimeOffset BaseInstant =
        DateTimeOffset.Parse("2026-06-30T10:30:00+00:00", CultureInfo.InvariantCulture);

    [Fact]
    public async Task GetByIdAsync_should_project_record_without_tracking()
    {
        await fixture.CleanAsync();
        FunctionalAuditRecord record = CreateRecord(metadata: new Dictionary<string, string>
        {
            ["amount"] = "100.00"
        });
        await SeedAsync(record);

        await using AuditDbContext context = fixture.CreateDbContext();
        var service = new FunctionalAuditRecordQueryService(context);

        AuditRecordReadModel? result = await service.GetByIdAsync(record.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(record.Id, result.Id);
        Assert.Equal("100.00", result.Metadata["amount"]);
        Assert.Equal("Client", result.Actor?.Type);
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task GetByOperationIdAsync_should_return_records_ordered_by_occurred_at_then_created_at()
    {
        await fixture.CleanAsync();
        await SeedAsync(
            CreateRecord(operationId: "op-1", status: "Succeeded", occurredAt: BaseInstant.AddMinutes(2)),
            CreateRecord(operationId: "op-1", status: "Received", occurredAt: BaseInstant),
            CreateRecord(operationId: "op-2", status: "Failed", occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(operationId: "op-1", status: "Rejected", occurredAt: BaseInstant.AddMinutes(1)));

        await using AuditDbContext context = fixture.CreateDbContext();
        var service = new FunctionalAuditRecordQueryService(context);

        IReadOnlyCollection<AuditRecordReadModel> result =
            await service.GetByOperationIdAsync("op-1", TestContext.Current.CancellationToken);

        Assert.Equal(["Received", "Rejected", "Succeeded"], [.. result.Select(x => x.Status)]);
        Assert.All(result, item => Assert.Equal("op-1", item.OperationId));
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task SearchAsync_should_filter_by_all_supported_fields_and_period()
    {
        await fixture.CleanAsync();
        await SeedAsync(
            CreateRecord(
                merchantId: "merchant-a",
                sourceService: "AuditProducerA",
                operationType: "OperationApproved",
                status: "Succeeded",
                entityType: "Transfer",
                entityId: "trf-1",
                occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(
                merchantId: "merchant-a",
                sourceService: "AuditProducerA",
                operationType: "OperationApproved",
                status: "Failed",
                entityType: "Transfer",
                entityId: "trf-1",
                occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(
                merchantId: "merchant-b",
                sourceService: "AuditProducerB",
                operationType: "OperationApproved",
                status: "Succeeded",
                entityType: "Transfer",
                entityId: "trf-1",
                occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(
                merchantId: "merchant-a",
                sourceService: "AuditProducerA",
                operationType: "OperationApproved",
                status: "Succeeded",
                entityType: "LedgerEntry",
                entityId: "trf-1",
                occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(
                merchantId: "merchant-a",
                sourceService: "AuditProducerA",
                operationType: "OperationApproved",
                status: "Succeeded",
                entityType: "Transfer",
                entityId: "trf-2",
                occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(
                merchantId: "merchant-a",
                sourceService: "AuditProducerA",
                operationType: "OperationApproved",
                status: "Succeeded",
                entityType: "Transfer",
                entityId: "trf-1",
                occurredAt: BaseInstant.AddDays(-1)));

        await using AuditDbContext context = fixture.CreateDbContext();
        var service = new FunctionalAuditRecordQueryService(context);

        PagedResult<AuditRecordReadModel> result = await service.SearchAsync(
            new AuditRecordSearchCriteria(
                MerchantId: "merchant-a",
                SourceService: "AuditProducerA",
                OperationType: "OperationApproved",
                Status: "Succeeded",
                EntityType: "Transfer",
                EntityId: "trf-1",
                From: BaseInstant,
                To: BaseInstant.AddHours(1),
                Page: 1,
                PageSize: 10),
            TestContext.Current.CancellationToken);

        AuditRecordReadModel item = Assert.Single(result.Items);
        Assert.Equal("merchant-a", item.MerchantId);
        Assert.Equal("AuditProducerA", item.SourceService);
        Assert.Equal("OperationApproved", item.OperationType);
        Assert.Equal("Succeeded", item.Status);
        Assert.Equal("Transfer", item.EntityType);
        Assert.Equal("trf-1", item.EntityId);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_should_order_descending_and_return_requested_page()
    {
        await fixture.CleanAsync();
        await SeedAsync(
            CreateRecord(operationType: "Oldest", occurredAt: BaseInstant),
            CreateRecord(operationType: "Middle", occurredAt: BaseInstant.AddMinutes(1)),
            CreateRecord(operationType: "Newest", occurredAt: BaseInstant.AddMinutes(2)));

        await using AuditDbContext context = fixture.CreateDbContext();
        var service = new FunctionalAuditRecordQueryService(context);

        PagedResult<AuditRecordReadModel> result = await service.SearchAsync(
            new AuditRecordSearchCriteria(
                MerchantId: null,
                SourceService: null,
                OperationType: null,
                Status: null,
                EntityType: null,
                EntityId: null,
                From: BaseInstant.AddMinutes(-1),
                To: BaseInstant.AddMinutes(3),
                Page: 2,
                PageSize: 1),
            TestContext.Current.CancellationToken);

        AuditRecordReadModel item = Assert.Single(result.Items);
        Assert.Equal("Middle", item.OperationType);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task SearchAsync_should_return_empty_page_when_no_record_matches()
    {
        await fixture.CleanAsync();
        await SeedAsync(CreateRecord(merchantId: "merchant-a"));

        await using AuditDbContext context = fixture.CreateDbContext();
        var service = new FunctionalAuditRecordQueryService(context);

        PagedResult<AuditRecordReadModel> result = await service.SearchAsync(
            new AuditRecordSearchCriteria(
                MerchantId: "merchant-b",
                SourceService: null,
                OperationType: null,
                Status: null,
                EntityType: null,
                EntityId: null,
                From: BaseInstant.AddMinutes(-1),
                To: BaseInstant.AddMinutes(1),
                Page: 1,
                PageSize: 10),
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    private async Task SeedAsync(params FunctionalAuditRecord[] records)
    {
        await using AuditDbContext context = fixture.CreateDbContext();
        await context.FunctionalAuditRecords.AddRangeAsync(records, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static FunctionalAuditRecord CreateRecord(
        string operationId = "operation-1",
        string sourceService = "AuditProducer",
        string operationType = "OperationCompleted",
        string status = "Succeeded",
        DateTimeOffset? occurredAt = null,
        string? entityType = "Transfer",
        string? entityId = "transfer-1",
        string? merchantId = "merchant-1",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        DateTimeOffset instant = occurredAt ?? BaseInstant;

        return FunctionalAuditRecord.Create(
            operationId: operationId,
            sourceService: sourceService,
            operationType: operationType,
            status: status,
            occurredAt: instant,
            correlationId: "correlation-1",
            idempotencyKey: Guid.NewGuid().ToString(),
            sourceEventId: Guid.NewGuid().ToString(),
            entityType: entityType,
            entityId: entityId,
            merchantId: merchantId,
            actorType: "Client",
            actorSubject: "audit-client",
            actorClientId: "audit-client-id",
            reason: "processed",
            metadata: metadata,
            createdAt: instant.AddSeconds(1));
    }
}
