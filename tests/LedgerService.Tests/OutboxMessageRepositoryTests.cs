using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Tests;

public sealed class OutboxMessageRepositoryTests
{
    [Fact]
    public async Task MarkSentAsync_DeveAtualizarStatusParaSent()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);

        var correlationId = Guid.NewGuid();

        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.Now.AddMinutes(-3), correlationId);

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        var processedAt = DateTime.Now;
        await repo.MarkSentAsync(msg.Id, processedAt);
        await db.SaveChangesAsync();

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(OutboxStatus.Sent, refreshed.Status);
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
            DateTime.Now,
            Guid.NewGuid(),
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "vendor=value",
            "tenant=poc");

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(msg.TraceParent, refreshed.TraceParent);
        Assert.Equal("vendor=value", refreshed.TraceState);
        Assert.Equal("tenant=poc", refreshed.Baggage);
    }

    [Fact]
    public async Task MarkFailedAttemptAsync_DeveIncrementarAttemptsEManterPendingAteMaxAttempts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var correlationId = Guid.NewGuid();
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.Now, correlationId);

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        await repo.MarkFailedAttemptAsync(msg.Id, maxAttempts: 3, nextAttemptAt: DateTime.Now.AddSeconds(10), lastError: "boom");
        await db.SaveChangesAsync();

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(1, refreshed.Attempts);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.NotNull(refreshed.NextAttemptAt);
        Assert.Equal("boom", refreshed.LastError);
    }

    [Fact]
    public async Task MarkFailedAttemptAsync_DeveMarcarFailedAoExcederMaxAttempts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var correlationId = Guid.NewGuid();
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated", "{}", DateTime.Now, correlationId);

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        await repo.MarkFailedAttemptAsync(msg.Id, maxAttempts: 1, nextAttemptAt: DateTime.Now.AddSeconds(10), lastError: "boom");
        await db.SaveChangesAsync();

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(1, refreshed.Attempts);
        Assert.Equal(OutboxStatus.Failed, refreshed.Status);
        Assert.Null(refreshed.NextAttemptAt);
        Assert.Equal("boom", refreshed.LastError);
        Assert.NotNull(refreshed.ProcessedAt);
    }

    [Fact]
    public async Task RequeueFailedAsync_DeveVoltarFailedParaPendingComAuditoria()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.Now, Guid.NewGuid());

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        await repo.MarkFailedAttemptAsync(msg.Id, maxAttempts: 1, nextAttemptAt: DateTime.Now.AddSeconds(10), lastError: "kafka down");
        await db.SaveChangesAsync();

        var requeuedAt = DateTime.Now;
        var requeued = await repo.RequeueFailedAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: requeuedAt,
            requeuedBy: "operador",
            reason: "broker recuperado");
        await db.SaveChangesAsync();

        Assert.Single(requeued);
        Assert.Equal(msg.Id, requeued[0].Id);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.Equal(0, refreshed.Attempts);
        Assert.Null(refreshed.NextAttemptAt);
        Assert.Null(refreshed.ProcessedAt);
        Assert.Equal(1, refreshed.RequeueCount);
        Assert.Equal(requeuedAt, refreshed.LastRequeuedAt);
        Assert.Equal("operador", refreshed.LastRequeuedBy);
        Assert.Equal("broker recuperado", refreshed.LastRequeueReason);
        Assert.Equal("kafka down", refreshed.LastError);
    }

    [Fact]
    public async Task RequeueFailedAsync_NaoDeveReprocessarSent()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.Now, Guid.NewGuid());
        msg.MarkSent(DateTime.Now);

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        var requeued = await repo.RequeueFailedAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: DateTime.Now,
            requeuedBy: "operador",
            reason: "nao deve reprocessar");
        await db.SaveChangesAsync();

        Assert.Empty(requeued);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(OutboxStatus.Sent, refreshed.Status);
        Assert.Equal(0, refreshed.RequeueCount);
    }

    [Fact]
    public async Task RequeueFailedAsync_NaoDeveReprocessarProcessingValida()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", DateTime.Now, Guid.NewGuid());
        msg.MarkProcessing("publisher", DateTime.Now.AddMinutes(5));

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();

        var requeued = await repo.RequeueFailedAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: DateTime.Now,
            requeuedBy: "operador",
            reason: "nao deve reprocessar");
        await db.SaveChangesAsync();

        Assert.Empty(requeued);

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(OutboxStatus.Processing, refreshed.Status);
        Assert.Equal("publisher", refreshed.LockOwner);
        Assert.Equal(0, refreshed.RequeueCount);
    }

    [Fact]
    public async Task RequeueFailedAsync_DevePermitirClaimEPublicacaoPosterior()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-db-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db);
        var now = DateTime.Now;
        var msg = new OutboxMessage("LedgerEntry", Guid.NewGuid(), "LedgerEntryCreated.v1", "{}", now.AddMinutes(-1), Guid.NewGuid());

        await repo.AddAsync(msg);
        await db.SaveChangesAsync();
        await repo.MarkFailedAttemptAsync(msg.Id, maxAttempts: 1, nextAttemptAt: now.AddSeconds(10), lastError: "kafka down");
        await db.SaveChangesAsync();

        await repo.RequeueFailedAsync(
            id: msg.Id,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 10,
            requeuedAt: now,
            requeuedBy: "operador",
            reason: "broker recuperado");
        await db.SaveChangesAsync();

        var claimed = await repo.ClaimPendingAsync(10, now, "publisher", TimeSpan.FromMinutes(1));
        await db.SaveChangesAsync();
        Assert.Single(claimed);
        Assert.Equal(msg.Id, claimed[0].Id);

        await repo.MarkSentAsync(msg.Id, now.AddSeconds(1));
        await db.SaveChangesAsync();

        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == msg.Id);
        Assert.Equal(OutboxStatus.Sent, refreshed.Status);
        Assert.Equal(1, refreshed.RequeueCount);
    }
}
