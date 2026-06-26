using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Persistence;
using IdentityService.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace IdentityService.IntegrationTests.Persistence;

[Trait("Category", "Container")]
[Trait("Category", "Integration")]
[Collection(PostgresIdentityCollection.Name)]
public sealed class IdentityPersistenceTests(PostgresIdentityFixture fixture) : IAsyncLifetime
{
    private readonly PostgresIdentityFixture _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task Repository_should_persist_user_with_runtime_identity_app_user()
    {
        await using var provider = _fixture.CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Application.Users.Ports.IUserRepository>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = CreateUser(email: "persisted@example.com", keycloakUserId: "kc-persisted");

        await repository.AddAsync(user, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(user.Email, persisted.Email);
        Assert.Equal(user.Username, persisted.Username);
        Assert.Equal(user.MerchantId, persisted.MerchantId);
        Assert.Equal(user.KeycloakUserId, persisted.KeycloakUserId);
    }

    [Fact]
    public async Task Database_should_enforce_unique_email()
    {
        await using var db = _fixture.CreateDbContext();

        db.Users.Add(CreateUser(email: "unique-email@example.com", keycloakUserId: "kc-email-1"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Users.Add(CreateUser(email: "unique-email@example.com", keycloakUserId: "kc-email-2"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_identity_users_email", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Database_should_enforce_unique_keycloak_user_id()
    {
        await using var db = _fixture.CreateDbContext();

        db.Users.Add(CreateUser(email: "keycloak-1@example.com", keycloakUserId: "kc-unique"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Users.Add(CreateUser(email: "keycloak-2@example.com", keycloakUserId: "kc-unique"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_identity_users_keycloak_user_id", postgresException.ConstraintName);
    }

    [Fact]
    public async Task Migration_should_create_users_table_in_identity_schema()
    {
        await using var db = _fixture.CreateDbContext();

        await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_schema = 'identity' AND table_name = 'users';
            """;

        var schema = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("identity", schema);
        Assert.Equal("identity", db.Model.GetDefaultSchema());
        Assert.Equal(
            "identity",
            db.Model.FindEntityType(typeof(User))?.GetSchema());
    }

    private static User CreateUser(string email, string keycloakUserId)
        => User.Register(
            UserId.New(),
            new Email(email),
            new Username("identity.user"),
            new MerchantId("merchant-shared"),
            keycloakUserId,
            new DateTime(2026, 06, 26, 12, 0, 0, DateTimeKind.Utc));
}
