using Microsoft.EntityFrameworkCore;

using TransferService.Infrastructure.Persistence;

namespace TransferService.IntegrationTests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class TransferServiceDbContextFactoryTests
{
    private const string EnvironmentVariableName = "TRANSFER_SERVICE_CONNECTION_STRING";

    [Fact]
    public void CreateDbContext_should_use_transfer_service_connection_string_environment_variable()
    {
        var previousValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        const string connectionString = "Host=localhost;Port=5432;Database=from_env;Username=transfer_migrator_user";

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, connectionString);

            using var db = new TransferServiceDbContextFactory().CreateDbContext([]);

            Assert.Equal(connectionString, db.Database.GetDbConnection().ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, previousValue);
        }
    }

    [Fact]
    public void CreateDbContext_should_use_postgresql_local_stack_fallback_without_hardcoded_password()
    {
        var previousValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            using var db = new TransferServiceDbContextFactory().CreateDbContext([]);

            var connectionString = db.Database.GetDbConnection().ConnectionString;
            Assert.Contains("Host=127.0.0.1", connectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Port=15432", connectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Database=appdb", connectionString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Username=transfer_migrator_user", connectionString, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password=", connectionString, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, previousValue);
        }
    }
}
