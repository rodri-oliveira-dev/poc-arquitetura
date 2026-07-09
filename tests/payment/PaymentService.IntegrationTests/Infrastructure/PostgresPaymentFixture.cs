using Microsoft.EntityFrameworkCore;

using PaymentService.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

namespace PaymentService.IntegrationTests.Infrastructure;

public sealed class PostgresPaymentFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("docker.io/postgres:16")
        .WithDatabase("appdb")
        .WithUsername("appuser")
        .WithPassword("app123")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task CleanAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE payment.idempotency_records, payment.payments;");
    }

    public PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "payment"))
            .Options;

        return new PaymentDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
