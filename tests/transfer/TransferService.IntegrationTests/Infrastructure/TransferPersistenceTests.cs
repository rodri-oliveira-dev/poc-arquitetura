using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Common.Exceptions;
using TransferService.Application.Transferencias.Commands;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;
using TransferService.Infrastructure.Messaging.Kafka;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;

namespace TransferService.IntegrationTests.Infrastructure;

[Trait("Category", "Container")]
[Trait("Category", "Integration")]
[Collection(PostgresTransferCollection.Name)]
public sealed class TransferPersistenceTests(PostgresTransferFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgresTransferFixture _fixture = fixture;

    [Fact]
    public async Task Should_persist_and_read_saga()
    {
        await _fixture.CleanAsync();
        var saga = CreateSaga("idem-persist", "hash-persist");

        await using (var db = _fixture.CreateDbContext())
        {
            db.TransferenciasSagas.Add(saga);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readDb = _fixture.CreateDbContext();
        var persisted = await readDb.TransferenciasSagas
            .AsNoTracking()
            .SingleAsync(x => x.Id == saga.Id, TestContext.Current.CancellationToken);

        Assert.Equal("merchant-source", persisted.SourceMerchantId.Value);
        Assert.Equal("merchant-destination", persisted.DestinationMerchantId.Value);
        Assert.Equal(100m, persisted.Amount.Value);
        Assert.Equal(TransferenciaSagaStatus.Pending, persisted.Status);
        Assert.Equal("idem-persist", persisted.IdempotencyKey);
        Assert.Equal("hash-persist", persisted.IdempotencyPayloadHash);
    }

    [Fact]
    public async Task Should_replay_idempotent_request_with_same_payload()
    {
        await _fixture.CleanAsync();
        await using var provider = _fixture.CreateServiceProvider(Now);
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SolicitarTransferenciaCommandHandler>();
        var command = ValidCommand("idem-same", 100m);

        var created = await handler.Handle(command, TestContext.Current.CancellationToken);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(created.TransferenciaId, replay.TransferenciaId);
        Assert.True(replay.IdempotentReplay);

        var db = scope.ServiceProvider.GetRequiredService<TransferServiceDbContext>();
        Assert.Equal(1, await db.TransferenciasSagas.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_reject_idempotent_request_with_different_payload()
    {
        await _fixture.CleanAsync();
        await using var provider = _fixture.CreateServiceProvider(Now);
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SolicitarTransferenciaCommandHandler>();

        await handler.Handle(ValidCommand("idem-conflict", 100m), TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(ValidCommand("idem-conflict", 150m), TestContext.Current.CancellationToken));

        Assert.Contains("different payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_write_outbox_in_same_transaction_as_saga()
    {
        await _fixture.CleanAsync();
        await using var provider = _fixture.CreateServiceProvider(Now);
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SolicitarTransferenciaCommandHandler>();

        var result = await handler.Handle(ValidCommand("idem-outbox", 100m), TestContext.Current.CancellationToken);

        var db = scope.ServiceProvider.GetRequiredService<TransferServiceDbContext>();
        var saga = await db.TransferenciasSagas.SingleAsync(TestContext.Current.CancellationToken);
        var outbox = await db.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(result.TransferenciaId, saga.Id);
        Assert.Equal(saga.Id, outbox.AggregateId);
        Assert.Equal("TransferenciaSaga", outbox.AggregateType);
        Assert.Equal(TransferenciaSolicitadaV1.Type, outbox.EventType);
        Assert.Equal(TransferenciaOutboxStatus.Pending, outbox.Status);
        Assert.Equal(result.TransferenciaId.ToString(), outbox.MessageKey);
        Assert.Equal("transfer.transferencia.solicitada", outbox.Topic);
    }

    [Fact]
    public void Should_map_event_type_to_kafka_topic_and_use_transferencia_id_as_key()
    {
        var mapper = new TransferenciaSagaKafkaMetadataMapper(
            Options.Create(new TransferenciaKafkaTopicOptions
            {
                Solicitada = "transfer-events",
                Falhou = "transfer-failures"
            }));
        var transferenciaId = Guid.NewGuid();

        var metadata = mapper.Map(new TransferenciaFalhouV1(
            transferenciaId,
            "source",
            "destination",
            100m,
            Now,
            "corr-1"));

        Assert.Equal("transfer-failures", metadata.Topic);
        Assert.Equal(transferenciaId.ToString(), metadata.MessageKey);
        Assert.Equal(TransferenciaFalhouV1.Type, metadata.Headers["event_type"]);
        Assert.Equal("corr-1", metadata.Headers["correlation_id"]);
    }

    [Fact]
    public async Task Should_query_pending_sagas_for_worker()
    {
        await _fixture.CleanAsync();
        Guid dueId;
        await using (var seedDb = _fixture.CreateDbContext())
        {
            var due = CreateSaga("idem-due", "hash-due");
            var future = CreateSaga("idem-future", "hash-future");
            future.ScheduleRetry(Now.AddHours(1), "retry later", Now);
            dueId = due.Id;
            seedDb.TransferenciasSagas.AddRange(due, future);
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var db = _fixture.CreateDbContext();
        var repository = new TransferService.Infrastructure.Persistence.Repositories.TransferenciaSagaRepository(db);
        var claimed = await repository.ClaimPendingAsync(
            10,
            Now,
            "worker-1",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        var saga = Assert.Single(claimed);
        Assert.Equal(dueId, saga.Id);
        Assert.Equal(TransferenciaSagaStatus.Processing, saga.Status);
        Assert.Equal("worker-1", saga.ProcessingLockOwner);
    }

    [Fact]
    public async Task Should_claim_saga_once_when_workers_compete()
    {
        await _fixture.CleanAsync();
        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.TransferenciasSagas.Add(CreateSaga("idem-lock", "hash-lock"));
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var db1 = _fixture.CreateDbContext();
        await using var tx1 = await db1.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var repo1 = new TransferService.Infrastructure.Persistence.Repositories.TransferenciaSagaRepository(db1);
        var first = await repo1.ClaimPendingAsync(
            1,
            Now,
            "worker-1",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        await using var db2 = _fixture.CreateDbContext();
        var repo2 = new TransferService.Infrastructure.Persistence.Repositories.TransferenciaSagaRepository(db2);
        var second = await repo2.ClaimPendingAsync(
            1,
            Now,
            "worker-2",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        await tx1.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task Migration_should_create_transfer_schema_tables()
    {
        await _fixture.CleanAsync();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'transfer'
              AND table_name IN ('transferencias_sagas', 'outbox_messages')
            ORDER BY table_name;
            """;

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(["outbox_messages", "transferencias_sagas"], tables);
    }

    private static TransferenciaSaga CreateSaga(string idempotencyKey, string payloadHash)
    {
        var saga = new TransferenciaSaga(
            new MerchantId("merchant-source"),
            new MerchantId("merchant-destination"),
            new TransferAmount(100m),
            Now);
        saga.RegisterRequestMetadata(idempotencyKey, payloadHash, "corr-1", Now);
        return saga;
    }

    private static SolicitarTransferenciaCommand ValidCommand(string idempotencyKey, decimal amount)
        => new(
            idempotencyKey,
            "merchant-source",
            "merchant-destination",
            amount,
            "corr-1");
}
