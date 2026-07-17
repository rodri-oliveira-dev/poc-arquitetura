using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace LedgerService.UnitTests.Domain.Entities;

public sealed class OutboxMessageClaimTests
{
    [Fact]
    public async Task ClaimPendingAsync_DeveBloquearSomenteMensagensElegiveisOrdenadasPorOcorrencia()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-claim-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options);
        var repo = new OutboxMessageRepository(db, TimeProvider.System);

        var now = new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc);
        var older = NewMessage(now.AddMinutes(-20));
        var newer = NewMessage(now.AddMinutes(-10));
        var scheduledForFuture = NewMessage(now.AddMinutes(-30));
        scheduledForFuture.MarkFailedPublishAttempt(maxRetries: 3, nextRetryAt: now.AddMinutes(5), lastError: "retry later");
        var alreadySent = NewMessage(now.AddMinutes(-40));
        alreadySent.MarkProcessed(now.AddMinutes(-1));

        await db.OutboxMessages.AddRangeAsync(newer, scheduledForFuture, older, alreadySent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var claimed = await repo.ClaimPendingAsync(
            batchSize: 2,
            now: now,
            lockOwner: "publisher-1",
            lockDuration: TimeSpan.FromMinutes(2),
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            claimed,
            first => Assert.Equal(older.Id, first.Id),
            second => Assert.Equal(newer.Id, second.Id));
        Assert.All(claimed, message =>
        {
            Assert.Equal(OutboxStatus.Processing, message.Status);
            Assert.Equal("publisher-1", message.LockOwner);
            Assert.Equal(now.AddMinutes(2), message.LockedUntil);
        });

        var unchangedFuture = await db.OutboxMessages.SingleAsync(x => x.Id == scheduledForFuture.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Pending, unchangedFuture.Status);
        Assert.Equal(now.AddMinutes(5), unchangedFuture.NextRetryAt);

        var unchangedSent = await db.OutboxMessages.SingleAsync(x => x.Id == alreadySent.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, unchangedSent.Status);
        Assert.Null(unchangedSent.LockOwner);
        Assert.Null(unchangedSent.LockedUntil);
    }

    private static OutboxMessage NewMessage(DateTime occurredAt)
    {
        return new OutboxMessage(
            aggregateType: "LedgerEntry",
            aggregateId: Guid.NewGuid(),
            eventType: "LedgerEntryCreated",
            payload: "{}",
            occurredAt: occurredAt,
            correlationId: Guid.NewGuid());
    }
}
