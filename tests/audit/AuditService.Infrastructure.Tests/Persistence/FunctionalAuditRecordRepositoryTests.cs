using System.Globalization;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Domain.FunctionalAuditing;
using AuditService.Infrastructure.Persistence;
using AuditService.Infrastructure.Persistence.Repositories;
using AuditService.Infrastructure.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Tests.Persistence;

[Collection(AuditPostgresCollection.Name)]
public sealed class FunctionalAuditRecordRepositoryTests(PostgresAuditFixture fixture)
{
    private static readonly DateTimeOffset BaseInstant =
        DateTimeOffset.Parse("2026-06-30T10:30:00+00:00", CultureInfo.InvariantCulture);

    [Fact]
    public async Task AddAsync_should_persist_functional_audit_record_with_metadata_and_timestamps()
    {
        await fixture.CleanAsync();
        FunctionalAuditRecord record = CreateRecord(
            idempotencyKey: "00000000-0000-0000-0000-000000000001",
            sourceEventId: "audit-event-1",
            metadata: new Dictionary<string, string>
            {
                ["amount"] = "100.00",
                ["currency"] = "BRL"
            });

        await using (AuditDbContext context = fixture.CreateDbContext())
        {
            var repository = new FunctionalAuditRecordRepository(context);

            await repository.AddAsync(record, TestContext.Current.CancellationToken);
            await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AuditDbContext verificationContext = fixture.CreateDbContext();
        FunctionalAuditRecord persisted = await verificationContext.FunctionalAuditRecords
            .SingleAsync(x => x.Id == record.Id, TestContext.Current.CancellationToken);

        Assert.Equal("operation-1", persisted.OperationId);
        Assert.Equal("AuditProducer", persisted.SourceService);
        Assert.Equal("100.00", persisted.Metadata["amount"]);
        Assert.Equal(BaseInstant, persisted.OccurredAt);
        Assert.Equal(BaseInstant.AddSeconds(5), persisted.CreatedAt);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_should_return_existing_record_without_tracking()
    {
        await fixture.CleanAsync();
        FunctionalAuditRecord record = CreateRecord(idempotencyKey: "idem-1");
        await SeedAsync(record);

        await using AuditDbContext context = fixture.CreateDbContext();
        var repository = new FunctionalAuditRecordRepository(context);

        FunctionalAuditRecord? result = await repository.GetByIdempotencyKeyAsync("idem-1", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(record.Id, result.Id);
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task GetBySourceEventIdAsync_should_return_existing_record_without_tracking()
    {
        await fixture.CleanAsync();
        FunctionalAuditRecord record = CreateRecord(sourceEventId: "event-1");
        await SeedAsync(record);

        await using AuditDbContext context = fixture.CreateDbContext();
        var repository = new FunctionalAuditRecordRepository(context);

        FunctionalAuditRecord? result = await repository.GetBySourceEventIdAsync("event-1", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(record.Id, result.Id);
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_should_return_null_when_record_does_not_exist()
    {
        await fixture.CleanAsync();
        await using AuditDbContext context = fixture.CreateDbContext();
        var repository = new FunctionalAuditRecordRepository(context);

        FunctionalAuditRecord? result = await repository.GetByIdempotencyKeyAsync("missing", TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task SaveChangesAsync_should_translate_duplicate_idempotency_key()
    {
        await fixture.CleanAsync();
        await SeedAsync(CreateRecord(idempotencyKey: "duplicate-idem"));
        await using AuditDbContext context = fixture.CreateDbContext();
        var repository = new FunctionalAuditRecordRepository(context);
        await repository.AddAsync(CreateRecord(idempotencyKey: "duplicate-idem"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IdempotencyKeyUniqueConstraintViolationException>(() =>
            repository.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    [Fact]
    public async Task SaveChangesAsync_should_translate_duplicate_source_event_id()
    {
        await fixture.CleanAsync();
        await SeedAsync(CreateRecord(sourceEventId: "duplicate-event"));
        await using AuditDbContext context = fixture.CreateDbContext();
        var repository = new FunctionalAuditRecordRepository(context);
        await repository.AddAsync(CreateRecord(sourceEventId: "duplicate-event"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<SourceEventIdUniqueConstraintViolationException>(() =>
            repository.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(context.ChangeTracker.Entries<FunctionalAuditRecord>());
    }

    private async Task SeedAsync(params FunctionalAuditRecord[] records)
    {
        await using AuditDbContext context = fixture.CreateDbContext();
        await context.FunctionalAuditRecords.AddRangeAsync(records, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static FunctionalAuditRecord CreateRecord(
        string operationId = "operation-1",
        string? idempotencyKey = null,
        string? sourceEventId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => FunctionalAuditRecord.Create(
            operationId: operationId,
            sourceService: "AuditProducer",
            operationType: "OperationCompleted",
            status: "Succeeded",
            occurredAt: BaseInstant,
            correlationId: "correlation-1",
            idempotencyKey: idempotencyKey,
            sourceEventId: sourceEventId,
            entityType: "Transfer",
            entityId: "transfer-1",
            merchantId: "merchant-1",
            actorType: "Client",
            actorSubject: "audit-client",
            actorClientId: "audit-client-id",
            reason: "processed",
            metadata: metadata,
            createdAt: BaseInstant.AddSeconds(5));
}
