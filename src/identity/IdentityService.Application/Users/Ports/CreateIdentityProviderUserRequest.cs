namespace IdentityService.Application.Users.Ports;

public sealed record CreateIdentityProviderUserRequest(
    string Email,
    string Username,
    string Password);
