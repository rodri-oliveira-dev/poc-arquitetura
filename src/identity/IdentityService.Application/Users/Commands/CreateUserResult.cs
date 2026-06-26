namespace IdentityService.Application.Users.Commands;

public sealed record CreateUserResult(
    Guid UserId,
    string Email,
    string Username,
    string MerchantId,
    string KeycloakUserId);
