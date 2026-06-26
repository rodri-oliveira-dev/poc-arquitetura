using IdentityService.Domain.Users;

namespace IdentityService.Application.Users.Ports;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
