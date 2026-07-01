using Microsoft.EntityFrameworkCore;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Domain.Sagas;
using TransferService.Infrastructure.Persistence.Outbox;

namespace TransferService.Infrastructure.Persistence;

public sealed class TransferServiceDbContext : DbContext, IUnitOfWork
{
    public DbSet<TransferenciaSaga> TransferenciasSagas => Set<TransferenciaSaga>();
    public DbSet<TransferenciaOutboxMessage> OutboxMessages => Set<TransferenciaOutboxMessage>();
    public DbSet<TransferenciaIdempotencyRecord> IdempotencyRecords => Set<TransferenciaIdempotencyRecord>();

    public TransferServiceDbContext(DbContextOptions<TransferServiceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("transfer");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransferServiceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new AppTransaction(transaction);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
        => base.SaveChangesAsync(cancellationToken);
}
