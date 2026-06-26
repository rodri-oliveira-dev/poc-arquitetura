using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await context.Users.AddAsync(user, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
