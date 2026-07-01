using AuditService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace AuditService.Infrastructure.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class AuditPostgresCollection : ICollectionFixture<PostgresAuditFixture>
{
    public const string Name = "Audit PostgreSQL";
}

public sealed class PostgresAuditFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("docker.io/postgres:16")
        .WithDatabase("appdb")
        .WithUsername("appuser")
        .WithPassword("app123")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using AuditDbContext db = CreateDbContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public AuditDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(
                _postgres.GetConnectionString(),
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", "audit")
                    .ConfigureDataSource(dataSourceBuilder => dataSourceBuilder.EnableDynamicJson()))
            .Options;

        return new AuditDbContext(options);
    }

    public async Task CleanAsync()
    {
        await using AuditDbContext db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE audit.functional_audit_records;",
            TestContext.Current.CancellationToken);
    }
}
