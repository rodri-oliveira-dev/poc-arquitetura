using IdentityService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace IdentityService.UnitTests.Infrastructure.Persistence;

public sealed class IdentityDbContextFactoryTests
{
    private const string ConnectionStringVariable = "IDENTITY_SERVICE_CONNECTION_STRING";

    [Fact]
    public void CreateDbContext_should_use_connection_string_from_environment_when_defined()
    {
        const string connectionString =
            "Host=localhost;Port=15432;Database=identity_test;Username=identity_user;Password=test";

        var previousValue = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, connectionString);

        try
        {
            using var context = new IdentityDbContextFactory().CreateDbContext([]);

            Assert.Contains("Database=identity_test", context.Database.GetConnectionString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, previousValue);
        }
    }

    [Fact]
    public void CreateDbContext_should_use_default_connection_string_when_environment_is_not_defined()
    {
        var previousValue = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, null);

        try
        {
            using var context = new IdentityDbContextFactory().CreateDbContext([]);

            Assert.Contains("Database=appdb", context.Database.GetConnectionString(), StringComparison.Ordinal);
            Assert.Contains("Username=identity_migrator_user", context.Database.GetConnectionString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, previousValue);
        }
    }

    [Fact]
    public void CreateDbContext_should_configure_npgsql_provider()
    {
        var previousValue = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, null);

        try
        {
            using var context = new IdentityDbContextFactory().CreateDbContext([]);

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, previousValue);
        }
    }
}
