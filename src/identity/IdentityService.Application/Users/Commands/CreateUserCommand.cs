namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Name,
    string Email,
    string Username,
    string Password,
    string? Document = null);
