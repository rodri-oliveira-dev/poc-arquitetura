using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.UnitTests.Tests;

public sealed class IdempotencyRecordRepositoryTests
{
    [Fact]
    public async Task GetByMerchantAndKeyAsync_DeveRetornarRegistroCorretoSemRastrearEntidade()
    {
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var repo = new IdempotencyRecordRepository(db);

        var ledgerEntryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var expiresAt = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var expected = new IdempotencyRecord(
            merchantId: "merchant-1",
            idempotencyKey: "idem-1",
            requestHash: "hash-1",
            ledgerEntryId: ledgerEntryId,
            responseStatusCode: 201,
            responseBody: """{"id":"entry-1"}""",
            expiresAt: expiresAt);
        var otherMerchantSameKey = new IdempotencyRecord(
            merchantId: "merchant-2",
            idempotencyKey: "idem-1",
            requestHash: "hash-2",
            ledgerEntryId: null,
            responseStatusCode: 201,
            responseBody: null,
            expiresAt: expiresAt);

        await db.IdempotencyRecords.AddRangeAsync(expected, otherMerchantSameKey);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await repo.GetByMerchantAndKeyAsync("merchant-1", "idem-1");

        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        Assert.Equal("merchant-1", result.MerchantId);
        Assert.Equal("idem-1", result.IdempotencyKey);
        Assert.Equal("hash-1", result.RequestHash);
        Assert.Equal(ledgerEntryId, result.LedgerEntryId);
        Assert.Equal(201, result.ResponseStatusCode);
        Assert.Equal("""{"id":"entry-1"}""", result.ResponseBody);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.Empty(db.ChangeTracker.Entries<IdempotencyRecord>());
    }

    [Fact]
    public async Task AddAsync_DevePersistirRegistroDeIdempotencia()
    {
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var repo = new IdempotencyRecordRepository(db);

        var expiresAt = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var record = new IdempotencyRecord(
            merchantId: "merchant-1",
            idempotencyKey: "idem-2",
            requestHash: "hash-2",
            ledgerEntryId: null,
            responseStatusCode: 409,
            responseBody: """{"error":"conflict"}""",
            expiresAt: expiresAt);

        await repo.AddAsync(record);
        await db.SaveChangesAsync();

        var saved = await db.IdempotencyRecords.SingleAsync();
        Assert.Equal(record.Id, saved.Id);
        Assert.Equal("merchant-1", saved.MerchantId);
        Assert.Equal("idem-2", saved.IdempotencyKey);
        Assert.Equal("hash-2", saved.RequestHash);
        Assert.Null(saved.LedgerEntryId);
        Assert.Equal(409, saved.ResponseStatusCode);
        Assert.Equal("""{"error":"conflict"}""", saved.ResponseBody);
        Assert.Equal(expiresAt, saved.ExpiresAt);
    }

    [Fact]
    public async Task GetByMerchantAndKeyAsync_DeveRetornarNullQuandoChaveNaoExisteParaMerchant()
    {
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var repo = new IdempotencyRecordRepository(db);

        await db.IdempotencyRecords.AddAsync(new IdempotencyRecord(
            merchantId: "merchant-1",
            idempotencyKey: "idem-1",
            requestHash: "hash-1",
            ledgerEntryId: null,
            responseStatusCode: 201,
            responseBody: null,
            expiresAt: new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await repo.GetByMerchantAndKeyAsync("merchant-2", "idem-1");

        Assert.Null(result);
        Assert.Empty(db.ChangeTracker.Entries<IdempotencyRecord>());
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"idempotency-repo-{Guid.NewGuid():N}")
            .Options;
    }
}
