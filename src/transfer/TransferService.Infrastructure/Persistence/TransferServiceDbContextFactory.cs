using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransferService.Infrastructure.Persistence;

public sealed class TransferServiceDbContextFactory : IDesignTimeDbContextFactory<TransferServiceDbContext>
{
    private const string DefaultConnectionString =
        "Host=127.0.0.1;Port=15432;Database=appdb;Username=transfer_migrator_user";

    public TransferServiceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TRANSFER_SERVICE_CONNECTION_STRING")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<TransferServiceDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "transfer"))
            .Options;

        return new TransferServiceDbContext(options);
    }
}
