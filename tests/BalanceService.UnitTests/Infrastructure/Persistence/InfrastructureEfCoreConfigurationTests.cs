using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace BalanceService.UnitTests.Infrastructure.Persistence;

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
        Assert.NotNull(model.FindEntityType(typeof(DailyBalance)));
        Assert.NotNull(model.FindEntityType(typeof(ProcessedEvent)));
        var daily = model.FindEntityType(typeof(DailyBalance))!;
        Assert.NotEmpty(daily.GetIndexes());
        var processed = model.FindEntityType(typeof(ProcessedEvent))!;
        Assert.NotEmpty(processed.GetIndexes());
    }
}
