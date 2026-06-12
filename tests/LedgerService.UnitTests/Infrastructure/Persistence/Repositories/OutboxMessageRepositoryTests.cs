using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.UnitTests.Infrastructure.Persistence.Repositories;

public sealed class OutboxMessageRepositoryTests
{
    [Fact]
    public async Task MarkProcessedAsync_DeveAtualizarStatusParaProcessed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);

        var correlationId = Guid.NewGuid();

        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.UtcNow.AddMinutes(-3), correlationId);

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var processedAt = DateTime.UtcNow;
        await repo.MarkProcessedAsync(msg.Id, processedAt, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, refreshed.Status);
        Assert.Equal(processedAt, refreshed.ProcessedAt);
    }

    [Fact]
    public async Task AddAsync_DevePersistirContextoW3cOpcional()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);

        var msg = new OutboxMessage(
            "LedgerEntry",
            Guid.NewGuid(),
            "LedgerEntryCreated.v1",
            "{}",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "vendor=value",
            "tenant=poc");

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(msg.TraceParent, refreshed.TraceParent);
        Assert.Equal("vendor=value", refreshed.TraceState);
        Assert.Equal("tenant=poc", refreshed.Baggage);
    }

    [Fact]
    public async Task MarkFailedPublishAttemptAsync_DeveIncrementarRetryCountEManterPendingAteMaxRetries()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var correlationId = Guid.NewGuid();
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.UtcNow, correlationId);

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await repo.MarkFailedPublishAttemptAsync(msg.Id, maxRetries: 3, nextRetryAt: DateTime.UtcNow.AddSeconds(10), lastError: "boom", TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.NotNull(refreshed.NextRetryAt);
        Assert.Equal("boom", refreshed.LastError);
    }

    [Fact]
    public async Task MarkFailedPublishAttemptAsync_DeveMarcarDeadLetterAoExcederMaxRetries()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var correlationId = Guid.NewGuid();
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.UtcNow, correlationId);

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await repo.MarkFailedPublishAttemptAsync(msg.Id, maxRetries: 1, nextRetryAt: DateTime.UtcNow.AddSeconds(10), lastError: "boom", TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.Equal(OutboxStatus.DeadLetter, refreshed.Status);
        Assert.Null(refreshed.NextRetryAt);
        Assert.Equal("boom", refreshed.LastError);
        Assert.Null(refreshed.ProcessedAt);
    }

    [Fact]
    public async Task RequeueDeadLettersAsync_DeveVoltarDeadLetterParaPendingComAuditoria()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.UtcNow, Guid.NewGuid());

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await repo.MarkFailedPublishAttemptAsync(msg.Id, maxRetries: 1, nextRetryAt: DateTime.UtcNow.AddSeconds(10), lastError: "kafka down", TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var requeuedAt = DateTime.UtcNow;
        var requeued = await repo.RequeueDeadLettersAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: requeuedAt,
            requeuedBy: "operador",
            reason: "broker recuperado",
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(requeued);
        Assert.Equal(msg.Id, requeued[0].Id);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.Equal(0, refreshed.RetryCount);
        Assert.Null(refreshed.NextRetryAt);
        Assert.Null(refreshed.ProcessedAt);
        Assert.Equal(1, refreshed.RequeueCount);
        Assert.Equal(requeuedAt, refreshed.LastRequeuedAt);
        Assert.Equal("operador", refreshed.LastRequeuedBy);
        Assert.Equal("broker recuperado", refreshed.LastRequeueReason);
        Assert.Null(refreshed.LastError);
    }

    [Fact]
    public async Task RequeueDeadLettersAsync_NaoDeveReprocessarProcessed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.UtcNow, Guid.NewGuid());
        msg.MarkProcessed(DateTime.UtcNow);

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var requeued = await repo.RequeueDeadLettersAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: DateTime.UtcNow,
            requeuedBy: "operador",
            reason: "nao deve reprocessar",
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(requeued);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, refreshed.Status);
        Assert.Equal(0, refreshed.RequeueCount);
    }

    [Fact]
    public async Task RequeueDeadLettersAsync_NaoDeveReprocessarProcessingValida()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.UtcNow, Guid.NewGuid());
        msg.MarkProcessing("publisher", DateTime.UtcNow.AddMinutes(5));

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var requeued = await repo.RequeueDeadLettersAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: DateTime.UtcNow,
            requeuedBy: "operador",
            reason: "nao deve reprocessar",
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(requeued);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processing, refreshed.Status);
        Assert.Equal("publisher", refreshed.LockOwner);
        Assert.Equal(0, refreshed.RequeueCount);
    }

    [Fact]
    public async Task RequeueDeadLettersAsync_DevePermitirClaimEPublicacaoPosterior()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var now = DateTime.UtcNow;
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", now.AddMinutes(-1), Guid.NewGuid());

        await repo.AddAsync(msg, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await repo.MarkFailedPublishAttemptAsync(msg.Id, maxRetries: 1, nextRetryAt: now.AddSeconds(10), lastError: "kafka down", TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await repo.RequeueDeadLettersAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: now,
            requeuedBy: "operador",
            reason: "broker recuperado",
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var claimed = await repo.ClaimPendingAsync(10, now, "publisher", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Single(claimed);
        Assert.Equal(msg.Id, claimed[0].Id);

        await repo.MarkProcessedAsync(msg.Id, now.AddSeconds(1), TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, refreshed.Status);
        Assert.Equal(1, refreshed.RequeueCount);
    }
}
