namespace IdentityService.Application.Users.Ports;

public sealed record CreateIdentityProviderUserRequest(
    string Name,
    string Email,
    string Username,
    string Password);
