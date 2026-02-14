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
}
