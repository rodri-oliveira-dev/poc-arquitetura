using IdentityService.Application.Common.DomainEvents;
using IdentityService.Domain.Common;
using IdentityService.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IDomainEventDispatcher? domainEventDispatcher = null) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public override int SaveChanges()
    {
        var entities = GetEntitiesWithDomainEvents();
        var domainEvents = GetDomainEvents(entities);

        var result = base.SaveChanges();

        DispatchDomainEventsAsync(domainEvents, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        ClearDomainEvents(entities);

        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entities = GetEntitiesWithDomainEvents();
        var domainEvents = GetDomainEvents(entities);

        var result = await base.SaveChangesAsync(cancellationToken);

        await DispatchDomainEventsAsync(domainEvents, cancellationToken);
        ClearDomainEvents(entities);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    private List<Entity> GetEntitiesWithDomainEvents()
        =>
        [
            .. ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
        ];

    private static List<IDomainEvent> GetDomainEvents(IEnumerable<Entity> entities)
        =>
        [
            .. entities
            .SelectMany(entity => entity.DomainEvents)
        ];

    private Task DispatchDomainEventsAsync(
        List<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
        => domainEvents.Count == 0 || domainEventDispatcher is null
            ? Task.CompletedTask
            : domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

    private static void ClearDomainEvents(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }
    }
}
