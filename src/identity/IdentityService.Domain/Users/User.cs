using IdentityService.Domain.Common;
using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.Users;

public sealed class User : AggregateRoot
{
    private User(
        UserId id,
        Email email,
        Username username,
        MerchantId merchantId,
        string keycloakUserId)
    {
        Id = id;
        Email = email;
        Username = username;
        MerchantId = merchantId;
        KeycloakUserId = keycloakUserId;
    }

    public UserId Id
    {
        get;
    }

    public Email Email
    {
        get;
    }

    public Username Username
    {
        get;
    }

    public MerchantId MerchantId
    {
        get;
    }

    public string KeycloakUserId
    {
        get;
    }

    public static User Register(
        UserId id,
        Email email,
        Username username,
        MerchantId merchantId,
        string keycloakUserId,
        DateTime occurredAt)
    {
        if (string.IsNullOrWhiteSpace(keycloakUserId))
            throw new DomainException("KeycloakUserId is required.");

        if (occurredAt.Kind != DateTimeKind.Utc)
            throw new DomainException("OccurredAt must be UTC.");

        var user = new User(id, email, username, merchantId, keycloakUserId.Trim());

        user.AddDomainEvent(new UserRegisteredDomainEvent(
            user.Id,
            user.Email,
            user.Username,
            user.MerchantId,
            user.KeycloakUserId,
            occurredAt));

        return user;
    }
}
