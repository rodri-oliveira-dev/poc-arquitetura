namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserResult(
    Guid Id,
    string KeycloakUserId,
    string MerchantId,
    string Username,
    string Email);
