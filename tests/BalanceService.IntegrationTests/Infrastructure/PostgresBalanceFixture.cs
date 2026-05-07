using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Commands;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace BalanceService.IntegrationTests.Infrastructure;

public sealed class PostgresBalanceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("docker.io/postgres:16")
        .WithDatabase("dbBalance")
        .WithUsername("userBalance")
        .WithPassword("Balance123")
        .WithPortBinding(15433, 5432)
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public BalanceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new BalanceDbContext(options);
    }

    public ServiceProvider CreateServiceProvider(DateTimeOffset now)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IClock>(new FixedClock(now));
        services.AddDbContext<BalanceDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BalanceDbContext>());
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<ApplyLedgerEntryCreatedHandler>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    public async Task CleanAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE processed_events, daily_balances;");
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
