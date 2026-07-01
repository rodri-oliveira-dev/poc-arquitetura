using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuditService.Infrastructure.Persistence;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    private const string DefaultConnectionString =
        "Host=127.0.0.1;Port=15432;Database=appdb;Username=audit_migrator_user";

    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUDIT_SERVICE_CONNECTION_STRING")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit"))
            .Options;

        return new AuditDbContext(options);
    }
}
