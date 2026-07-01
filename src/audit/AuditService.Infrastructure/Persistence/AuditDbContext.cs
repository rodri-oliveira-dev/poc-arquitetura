using AuditService.Domain.FunctionalAuditing;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<FunctionalAuditRecord> FunctionalAuditRecords => Set<FunctionalAuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
