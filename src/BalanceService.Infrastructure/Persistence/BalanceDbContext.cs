using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Domain.Balances;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.Infrastructure.Persistence;

public sealed class BalanceDbContext : DbContext, IUnitOfWork
{
    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    public BalanceDbContext(DbContextOptions<BalanceDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BalanceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var tx = await Database.BeginTransactionAsync(cancellationToken);
        return new AppTransaction(tx);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
        => base.SaveChangesAsync(cancellationToken);
}
