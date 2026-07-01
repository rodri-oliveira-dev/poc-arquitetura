using System.Globalization;

using AuditService.Application.Abstractions.Persistence;
using AuditService.Application.Common.Exceptions;
using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Application.Tests.FunctionalAuditing;

public sealed class CreateAuditRecordCommandHandlerTests
{
    [Fact]
    public async Task Handle_should_create_functional_audit_record()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = new CreateAuditRecordCommandHandler(repository);

        CreateAuditRecordResult result = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        FunctionalAuditRecord record = Assert.Single(repository.Records);
        Assert.Equal(result.Id, record.Id);
        Assert.Equal("LedgerService", record.SourceService);
        Assert.Equal("LancamentoCriado", record.OperationType);
        Assert.Equal("Succeeded", record.Status);
        Assert.Equal("100.00", record.Metadata["amount"]);
    }

    [Fact]
    public async Task Handle_should_return_existing_id_for_same_idempotency_key_and_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = new CreateAuditRecordCommandHandler(repository);

        CreateAuditRecordResult first = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);
        CreateAuditRecordResult second = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repository.Records);
    }

    [Fact]
    public async Task Handle_should_reject_same_idempotency_key_with_different_payload()
    {
        var repository = new FakeFunctionalAuditRecordRepository();
        var handler = new CreateAuditRecordCommandHandler(repository);

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

    private sealed class FakeFunctionalAuditRecordRepository : IFunctionalAuditRecordRepository
    {
        private readonly List<FunctionalAuditRecord> _records = [];

        public IReadOnlyCollection<FunctionalAuditRecord> Records => _records;

        public Task<FunctionalAuditRecord?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_records.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey));

        public Task AddAsync(FunctionalAuditRecord record, CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
