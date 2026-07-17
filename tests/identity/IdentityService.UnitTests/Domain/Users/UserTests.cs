using IdentityService.Domain.Common;
using IdentityService.Domain.Exceptions;
using IdentityService.Domain.Users;

namespace IdentityService.UnitTests.Domain.Users;

public sealed class UserTests
{
    private static readonly DateTime OccurredAt = new(2026, 06, 26, 12, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void Register_should_create_valid_user()
    {
        var user = CreateUser();

        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.Equal("user@example.com", user.Email.Value);
        Assert.Equal("user-name", user.Username.Value);
        Assert.Equal("merchant-1", user.MerchantId.Value);
        Assert.Equal("keycloak-user-1", user.KeycloakUserId);
    }

    [Fact]
    public void Register_should_reject_invalid_email()
    {
        var exception = Assert.Throws<DomainException>(() =>
            User.Register(
                UserId.New(),
                new Email("invalid-email"),
                new Username("user-name"),
                new MerchantId("merchant-1"),
                "keycloak-user-1",
                OccurredAt));

        Assert.Contains("Email", exception.Message);
    }

    [Fact]
    public void Register_should_reject_empty_merchant_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            User.Register(
                UserId.New(),
                new Email("user@example.com"),
                new Username("user-name"),
                new MerchantId(" "),
                "keycloak-user-1",
                OccurredAt));

        Assert.Contains("MerchantId", exception.Message);
    }

    [Fact]
    public void Register_should_reject_merchant_id_longer_than_100_characters()
    {
        var exception = Assert.Throws<DomainException>(() =>
            User.Register(
                UserId.New(),
                new Email("user@example.com"),
                new Username("user-name"),
                new MerchantId(new string('m', MerchantId.MaxLength + 1)),
                "keycloak-user-1",
                OccurredAt));

        Assert.Contains("100", exception.Message);
    }

    [Fact]
    public void MerchantId_should_accept_value_at_maximum_length()
    {
        var value = new string('m', MerchantId.MaxLength);

        var merchantId = new MerchantId(value);

        Assert.Equal(MerchantId.MaxLength, merchantId.Value.Length);
        Assert.Equal(value, merchantId.ToString());
    }

    [Fact]
    public void MerchantId_should_trim_value_and_reject_empty_result()
    {
        var merchantId = new MerchantId(" merchant-1 ");

        Assert.Equal("merchant-1", merchantId.Value);

        var exception = Assert.Throws<DomainException>(() => new MerchantId("\t"));
        Assert.Contains("MerchantId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_should_add_user_registered_domain_event()
    {
        var occurredAt = OccurredAt;

        var user = CreateUser(occurredAt);

        var domainEvent = Assert.IsType<UserRegisteredDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, domainEvent.UserId);
        Assert.Equal(user.Email, domainEvent.Email);
        Assert.Equal(user.Username, domainEvent.Username);
        Assert.Equal(user.MerchantId, domainEvent.MerchantId);
        Assert.Equal(user.KeycloakUserId, domainEvent.KeycloakUserId);
        Assert.Equal(occurredAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void User_should_not_expose_password()
    {
        var passwordProperties = typeof(User)
            .GetProperties()
            .Where(property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(passwordProperties);
    }

    [Fact]
    public void Entity_should_clear_domain_events()
    {
        var user = CreateUser();

        user.ClearDomainEvents();

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Domain_event_contract_should_expose_occurred_at()
    {
        var domainEvent = Assert.IsType<IDomainEvent>(Assert.Single(CreateUser().DomainEvents), exactMatch: false);

        Assert.Equal(OccurredAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void Register_should_reject_non_utc_occurred_at()
    {
        var exception = Assert.Throws<DomainException>(() =>
            User.Register(
                UserId.New(),
                new Email("user@example.com"),
                new Username("user-name"),
                new MerchantId("merchant-1"),
                "keycloak-user-1",
                new DateTime(2026, 06, 26, 12, 00, 00, DateTimeKind.Local)));

        Assert.Contains("UTC", exception.Message, StringComparison.Ordinal);
    }

    private static User CreateUser(DateTime? occurredAt = null)
        => User.Register(
            UserId.New(),
            new Email("user@example.com"),
            new Username("user-name"),
            new MerchantId("merchant-1"),
            "keycloak-user-1",
            occurredAt ?? OccurredAt);
}
