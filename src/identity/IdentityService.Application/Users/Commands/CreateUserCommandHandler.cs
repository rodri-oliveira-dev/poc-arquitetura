using IdentityService.Application.Users.Ports;
using IdentityService.Domain.Users;

namespace IdentityService.Application.Users.Commands;

public sealed class CreateUserCommandHandler(
    IIdentityProviderUserService identityProvider,
    IUserRepository users,
    IMerchantIdGenerator merchantIdGenerator)
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var identityUser = await identityProvider.CreateUserAsync(
            new CreateIdentityProviderUserRequest(
                command.Name,
                command.Email,
                command.Username,
                command.Password),
            cancellationToken);

        var user = User.Register(
            UserId.New(),
            new Email(command.Email),
            new Username(command.Username),
            new MerchantId(merchantIdGenerator.Generate()),
            identityUser.KeycloakUserId);

        try
        {
            await users.AddAsync(user, cancellationToken);
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await identityProvider.DeleteUserAsync(identityUser.KeycloakUserId, CancellationToken.None);
            throw;
        }

        return new CreateUserResult(
            user.Id.Value,
            user.KeycloakUserId,
            user.MerchantId.Value,
            user.Username.Value,
            user.Email.Value);
    }
}
