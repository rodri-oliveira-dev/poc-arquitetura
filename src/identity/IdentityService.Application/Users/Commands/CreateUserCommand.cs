namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Email,
    string Username,
    string KeycloakUserId);
