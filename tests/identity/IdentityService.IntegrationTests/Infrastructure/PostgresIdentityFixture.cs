using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Testcontainers.PostgreSql;

namespace IdentityService.IntegrationTests.Infrastructure;

public sealed class PostgresIdentityFixture : IAsyncLifetime
{
    private const string DatabaseName = "appdb";
    private const string AdminUser = "postgres_admin";
    private const string RuntimeUser = "identity_app_user";
    private const string MigratorUser = "identity_migrator_user";
    private const string Password = "local_dev_password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("docker.io/postgres:16")
        .WithDatabase(DatabaseName)
        .WithUsername(AdminUser)
        .WithPassword(Password)
        .Build();

    public string RuntimeConnectionString => BuildConnectionString(RuntimeUser, Password);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await ConfigureRolesAndSchemaAsync();

        await using var db = CreateDbContext(BuildConnectionString(MigratorUser, Password));
        await db.Database.MigrateAsync();

        await ConfigureIdentityGrantsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public IdentityDbContext CreateDbContext()
        => CreateDbContext(RuntimeConnectionString);

    public ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = RuntimeConnectionString
            })
            .Build();

        services.AddIdentityInfrastructure(configuration);
        configureServices?.Invoke(services);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public async Task CleanAsync()
    {
        await using var db = CreateDbContext(BuildConnectionString(MigratorUser, Password));
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.idempotency_records, identity.users;");
    }

    private static IdentityDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

        return new IdentityDbContext(options);
    }

    private async Task ConfigureRolesAndSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE identity_app_user LOGIN PASSWORD 'local_dev_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
            CREATE ROLE identity_migrator_user LOGIN PASSWORD 'local_dev_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;

            CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION identity_migrator_user;
            ALTER SCHEMA identity OWNER TO identity_migrator_user;
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;

            ALTER ROLE identity_app_user SET search_path = identity;
            ALTER ROLE identity_migrator_user SET search_path = identity;

            REVOKE ALL ON SCHEMA identity FROM PUBLIC;
            GRANT USAGE ON SCHEMA identity TO identity_app_user;
            GRANT USAGE, CREATE ON SCHEMA identity TO identity_migrator_user;

            ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO identity_app_user;
            ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
                GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO identity_app_user;
            """;

        await command.ExecuteNonQueryAsync();
    }

    private async Task ConfigureIdentityGrantsAsync()
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(MigratorUser, Password));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO identity_app_user;
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA identity TO identity_app_user;
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
}
