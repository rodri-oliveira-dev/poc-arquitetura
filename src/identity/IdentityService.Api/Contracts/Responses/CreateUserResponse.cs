namespace IdentityService.Api.Contracts.Responses;

public sealed record CreateUserResponse(
    Guid Id,
    string KeycloakUserId,
    string MerchantId,
    string Username,
    string Email);
