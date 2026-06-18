using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Commands;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Npgsql;

using Testcontainers.PostgreSql;

namespace BalanceService.IntegrationTests.Infrastructure;

public sealed class PostgresBalanceFixture : IAsyncLifetime
{
    private const string DatabaseName = "appdb";
    private const string AdminUser = "postgres_admin";
    private const string MigratorUser = "balance_migrator_user";
    private const string ReadUser = "balance_read_user";
    private const string WriteUser = "balance_write_user";
    private const string Password = "local_dev_password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("docker.io/postgres:16")
        .WithDatabase(DatabaseName)
        .WithUsername(AdminUser)
        .WithPassword(Password)
        .Build();

    public string WriteConnectionString => BuildConnectionString(WriteUser, Password);

    public string ReadConnectionString => BuildConnectionString(ReadUser, Password);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await ConfigureRolesAndSchemaAsync();

        await using var db = CreateDbContext(BuildConnectionString(MigratorUser, Password));
        await db.Database.MigrateAsync();

        await ConfigureBalanceGrantsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public BalanceDbContext CreateDbContext()
        => CreateDbContext(WriteConnectionString);

    public BalanceDbContext CreateReadOnlyDbContext()
        => CreateDbContext(ReadConnectionString);

    public ServiceProvider CreateServiceProvider(DateTimeOffset now)
        => CreateServiceProvider(now, WriteConnectionString);

    public async Task CleanAsync()
    {
        await using var db = CreateDbContext(BuildConnectionString(MigratorUser, Password));
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE balance.processed_events, balance.daily_balances;");
    }

    private static BalanceDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "balance"))
            .Options;

        return new BalanceDbContext(options);
    }

    private static ServiceProvider CreateServiceProvider(DateTimeOffset now, string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IClock>(new FixedClock(now));
        services.AddDbContext<BalanceDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "balance")));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BalanceDbContext>());
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<ApplyLedgerEntryCreatedHandler>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task ConfigureRolesAndSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE balance_read_user LOGIN PASSWORD 'local_dev_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
            CREATE ROLE balance_write_user LOGIN PASSWORD 'local_dev_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
            CREATE ROLE balance_migrator_user LOGIN PASSWORD 'local_dev_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;

            CREATE SCHEMA IF NOT EXISTS balance AUTHORIZATION balance_migrator_user;
            ALTER SCHEMA balance OWNER TO balance_migrator_user;
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;

            ALTER ROLE balance_read_user SET search_path = balance;
            ALTER ROLE balance_write_user SET search_path = balance;
            ALTER ROLE balance_migrator_user SET search_path = balance;

            REVOKE ALL ON SCHEMA balance FROM PUBLIC;
            GRANT USAGE ON SCHEMA balance TO balance_read_user, balance_write_user;
            GRANT USAGE, CREATE ON SCHEMA balance TO balance_migrator_user;

            ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
                GRANT SELECT ON TABLES TO balance_read_user;
            ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO balance_write_user;
            ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
                GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO balance_write_user;
            """;

        await command.ExecuteNonQueryAsync();
    }

    private async Task ConfigureBalanceGrantsAsync()
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(MigratorUser, Password));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            GRANT SELECT ON ALL TABLES IN SCHEMA balance TO balance_read_user;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA balance TO balance_write_user;
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA balance TO balance_write_user;
            """;

        await command.ExecuteNonQueryAsync();
    }

    private string BuildConnectionString(string username, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = DatabaseName,
            Username = username,
            Password = password
        };

        return builder.ConnectionString;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }
}
