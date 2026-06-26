namespace IdentityService.Application.Users.Ports;

public interface IIdentityProviderUserService
{
    Task<CreateIdentityProviderUserResult> CreateUserAsync(
        CreateIdentityProviderUserRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default);
}
