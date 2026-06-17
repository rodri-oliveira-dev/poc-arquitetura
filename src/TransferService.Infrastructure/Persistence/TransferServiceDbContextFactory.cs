using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransferService.Infrastructure.Persistence;

public sealed class TransferServiceDbContextFactory : IDesignTimeDbContextFactory<TransferServiceDbContext>
{
    public TransferServiceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransferServiceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=poc_arquitetura;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "transfer"))
            .Options;

        return new TransferServiceDbContext(options);
    }
}
