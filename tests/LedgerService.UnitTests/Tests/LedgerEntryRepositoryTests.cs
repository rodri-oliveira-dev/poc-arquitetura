using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.UnitTests.Tests;

public sealed class LedgerEntryRepositoryTests
{
    [Fact]
    public async Task AddAsync_DevePersistirLancamentoComDadosDoDominio()
    {
        var options = CreateOptions();
        await using var db = new AppDbContext(options);
        var repo = new LedgerEntryRepository(db);

        var occurredAt = new DateTime(2026, 2, 16, 10, 30, 0, DateTimeKind.Utc);
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var entry = new LedgerEntry(
            merchantId: " merchant-1 ",
            type: LedgerEntryType.Credit,
            amount: 150.75m,
            occurredAt: occurredAt,
            description: " venda balcao ",
            externalReference: " ext-123 ",
            correlationId: correlationId);

        await repo.AddAsync(entry);
        await db.SaveChangesAsync();

        var saved = await db.LedgerEntries.SingleAsync();
        Assert.Equal(entry.Id, saved.Id);
        Assert.Equal("merchant-1", saved.MerchantId);
        Assert.Equal(LedgerEntryType.Credit, saved.Type);
        Assert.Equal(150.75m, saved.Amount);
        Assert.Equal(occurredAt, saved.OccurredAt);
        Assert.Equal("venda balcao", saved.Description);
        Assert.Equal("ext-123", saved.ExternalReference);
        Assert.Equal(correlationId, saved.CorrelationId);
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ledger-entry-repo-{Guid.NewGuid():N}")
            .Options;
    }
}
