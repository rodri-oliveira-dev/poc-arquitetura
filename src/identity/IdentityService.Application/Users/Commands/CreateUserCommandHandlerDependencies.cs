using IdentityService.Application.Users.Ports;

namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserCommandHandlerDependencies(
    IIdentityProviderUserService IdentityProvider,
    IUserRepository Users,
    IMerchantIdGenerator MerchantIdGenerator,
    TimeProvider TimeProvider);
