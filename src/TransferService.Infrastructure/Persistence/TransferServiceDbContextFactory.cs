using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransferService.Infrastructure.Persistence;

public sealed class TransferServiceDbContextFactory : IDesignTimeDbContextFactory<TransferServiceDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=poc_arquitetura;Username=postgres";

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
