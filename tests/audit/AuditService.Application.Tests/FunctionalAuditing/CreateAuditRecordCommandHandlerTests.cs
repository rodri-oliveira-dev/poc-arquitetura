using System.Globalization;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.Common.Exceptions;
using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Application.Tests.FunctionalAuditing;

public sealed class CreateAuditRecordCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 06, 30, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Handle_should_create_functional_audit_record()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = CreateHandler(repository);

        CreateAuditRecordResult result = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        FunctionalAuditRecord record = Assert.Single(repository.Records);
        Assert.Equal(result.Id, record.Id);
        Assert.Equal("LedgerService", record.SourceService);
        Assert.Equal("LancamentoCriado", record.OperationType);
        Assert.Equal("Succeeded", record.Status);
        Assert.Equal(Now, record.CreatedAt);
        Assert.Equal("100.00", record.Metadata["amount"]);
    }

    [Fact]
    public async Task Handle_should_return_existing_id_for_same_idempotency_key_and_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = CreateHandler(repository);

        CreateAuditRecordResult first = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);
        CreateAuditRecordResult second = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repository.Records);
    }

    [Fact]
    public async Task Handle_should_reject_same_idempotency_key_with_different_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = CreateHandler(repository);

        await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        var differentPayload = ValidCommand() with
        {
            OperationType = "LancamentoEstornado"
        };

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(differentPayload, TestContext.Current.CancellationToken));

        Assert.Contains("Idempotency-Key already used", exception.Message);
        Assert.Single(repository.Records);
    }

    [Fact]
    public async Task Handle_should_return_existing_id_for_same_source_event_id_and_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = CreateHandler(repository);
        var command = ValidCommand() with
        {
            IdempotencyKey = null,
            SourceEventId = Guid.Parse("00000000-0000-0000-0000-000000000099")
        };

        CreateAuditRecordResult first = await handler.Handle(command, TestContext.Current.CancellationToken);
        CreateAuditRecordResult second = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repository.Records);
    }

    [Fact]
    public async Task Handle_should_return_existing_id_when_concurrent_save_collides_with_same_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository
        {
            ThrowIdempotencyCollisionOnSave = true
        };
        var command = ValidCommand();
        FunctionalAuditRecord existing = CreateRecord(command);
        repository.ExistingAfterCollision = existing;
        var handler = CreateHandler(repository);

        CreateAuditRecordResult result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(2, repository.GetByIdempotencyKeyCalls);
        Assert.Equal(1, repository.AddCalls);
    }

    [Fact]
    public async Task Handle_should_reject_when_concurrent_save_collides_with_different_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository
        {
            ThrowIdempotencyCollisionOnSave = true
        };
        var command = ValidCommand();
        repository.ExistingAfterCollision = CreateRecord(command with
        {
            OperationType = "LancamentoEstornado"
        });
        var handler = CreateHandler(repository);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Idempotency-Key already used", exception.Message);
        Assert.Equal(2, repository.GetByIdempotencyKeyCalls);
        Assert.Equal(1, repository.AddCalls);
    }

    private static CreateAuditRecordCommand ValidCommand()
        => new(
            OperationId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CorrelationId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            IdempotencyKey: "00000000-0000-0000-0000-000000000003",
            SourceService: "LedgerService",
            OperationType: "LancamentoCriado",
            EntityType: "Lancamento",
            EntityId: "lan_123",
            MerchantId: "m1",
            Actor: new CreateAuditRecordActor("Client", "poc-automation", "poc-automation"),
            Status: "Succeeded",
            Reason: null,
            Metadata: new Dictionary<string, string>
            {
                ["amount"] = "100.00",
                ["currency"] = "BRL"
            },
            OccurredAt: DateTimeOffset.Parse("2026-06-30T10:30:00Z", CultureInfo.InvariantCulture));

    private static CreateAuditRecordCommandHandler CreateHandler(FakeFunctionalAuditRecordRepository repository)
        => new(repository, new FixedClock(Now));

    private static FunctionalAuditRecord CreateRecord(CreateAuditRecordCommand command)
        => FunctionalAuditRecord.Create(
            operationId: command.OperationId.ToString(),
            sourceService: command.SourceService,
            operationType: command.OperationType,
            status: command.Status,
            occurredAt: command.OccurredAt,
            correlationId: command.CorrelationId?.ToString(),
            idempotencyKey: command.IdempotencyKey,
            sourceEventId: command.SourceEventId?.ToString(),
            entityType: command.EntityType,
            entityId: command.EntityId,
            merchantId: command.MerchantId,
            actorType: command.Actor?.Type,
            actorSubject: command.Actor?.Subject,
            actorClientId: command.Actor?.ClientId,
            reason: command.Reason,
            metadata: command.Metadata,
            createdAt: Now);

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeFunctionalAuditRecordRepository : IFunctionalAuditRecordRepository
    {
        private readonly List<FunctionalAuditRecord> _records = [];

        public IReadOnlyCollection<FunctionalAuditRecord> Records => _records;

        public bool ThrowIdempotencyCollisionOnSave
        {
            get; init;
        }

        public FunctionalAuditRecord? ExistingAfterCollision
        {
            get; set;
        }

        public int GetByIdempotencyKeyCalls
        {
            get; private set;
        }

        public int GetBySourceEventIdCalls
        {
            get; private set;
        }

        public int AddCalls
        {
            get; private set;
        }

        public Task<FunctionalAuditRecord?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            GetByIdempotencyKeyCalls++;

            FunctionalAuditRecord? record = _records.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
            if (record is null && GetByIdempotencyKeyCalls > 1)
                record = ExistingAfterCollision;

            return Task.FromResult(record);
        }

        public Task<FunctionalAuditRecord?> GetBySourceEventIdAsync(
            string sourceEventId,
            CancellationToken cancellationToken = default)
        {
            GetBySourceEventIdCalls++;
            FunctionalAuditRecord? record = _records.SingleOrDefault(x => x.SourceEventId == sourceEventId);
            return Task.FromResult(record);
        }

        public Task AddAsync(FunctionalAuditRecord record, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!ThrowIdempotencyCollisionOnSave)
                return Task.CompletedTask;

            _records.Clear();
            throw new IdempotencyKeyUniqueConstraintViolationException();
        }
    }
}
