using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.UnitTests.Tests;

public sealed class InfrastructureEfCoreConfigurationTests
{
    [Fact]
    public void BalanceDbContext_should_build_model_and_include_expected_entities_and_indexes()
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseInMemoryDatabase($"balance-model-{Guid.NewGuid():N}")
            .Options;

        using var db = new BalanceDbContext(options);

        // dispara OnModelCreating
        var model = db.Model;

        model.FindEntityType(typeof(DailyBalance)).Should().NotBeNull();
        model.FindEntityType(typeof(ProcessedEvent)).Should().NotBeNull();

        var daily = model.FindEntityType(typeof(DailyBalance))!;
        daily.GetIndexes().Should().NotBeEmpty();

        var processed = model.FindEntityType(typeof(ProcessedEvent))!;
        processed.GetIndexes().Should().NotBeEmpty();
    }
}
