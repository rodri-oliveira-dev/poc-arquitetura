using IdentityService.Domain.Common;

namespace IdentityService.Domain.Users;

public sealed record UserRegisteredDomainEvent(
    UserId UserId,
    Email Email,
    Username Username,
    MerchantId MerchantId,
    string KeycloakUserId,
    DateTime OccurredAt) : IDomainEvent;
