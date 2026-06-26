using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using IdentityService.IntegrationTests.Infrastructure;
using IdentityService.IntegrationTests.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;

namespace IdentityService.IntegrationTests.Api.Users;

[Trait("Category", "Container")]
[Trait("Category", "Integration")]
[Collection(PostgresIdentityCollection.Name)]
public sealed class UserEndpointsTests(PostgresIdentityFixture fixture) : IAsyncLifetime
{
    private readonly PostgresIdentityFixture _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.CleanAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task Post_users_should_return_201_for_valid_registration()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                username = "ana.identity",
                name = "Ana Identity",
                email = "ana.identity@example.com",
                password = "StrongPassword123!",
                document = "12345678900"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        var root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("id").GetGuid());
        Assert.StartsWith("kc-", root.GetProperty("keycloakUserId").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("merchantId").GetString()));
        Assert.Equal("ana.identity", root.GetProperty("username").GetString());
        Assert.Equal("ana.identity@example.com", root.GetProperty("email").GetString());

        var emailMessage = Assert.Single(factory.EmailSender.Messages);
        Assert.Equal("ana.identity@example.com", emailMessage.ToAddress);
        Assert.Equal("ana.identity", emailMessage.ToName);
        Assert.Equal("Bem-vindo", emailMessage.Subject);
        Assert.Contains("Bem-vindo, ana.identity", emailMessage.HtmlBody, StringComparison.Ordinal);

        var identityProviderRequest = Assert.Single(factory.IdentityProvider.CreateRequests);
        Assert.Equal("Ana Identity", identityProviderRequest.Name);
        Assert.Equal("ana.identity@example.com", identityProviderRequest.Email);
        Assert.Equal("ana.identity", identityProviderRequest.Username);

        await using var db = _fixture.CreateDbContext();
        var persistedUsers = await db.Users.ToListAsync(TestContext.Current.CancellationToken);
        var persisted = Assert.Single(
            persistedUsers,
            user => user.Email.Value == "ana.identity@example.com");

        Assert.Equal("ana.identity", persisted.Username.Value);
        Assert.Equal(root.GetProperty("merchantId").GetString(), persisted.MerchantId.Value);
        Assert.Equal(root.GetProperty("keycloakUserId").GetString(), persisted.KeycloakUserId);
    }

    [Fact]
    public async Task Post_users_should_keep_registration_valid_when_welcome_email_is_not_sent()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                username = "email.failure",
                name = "Email Failure",
                email = "email.failure@example.com",
                password = "StrongPassword123!",
                document = "12345678900"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_users_should_return_400_for_invalid_request()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                username = "",
                name = "Invalid User",
                email = "not-an-email",
                password = "short"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_users_should_return_401_without_token()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            ValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_users_should_return_403_without_identity_write_scope()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.read");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            ValidRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(PostgresIdentityApiFactory factory, string scopes)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.CreateToken(scopes: scopes));

        return client;
    }

    private static object ValidRequest()
        => new
        {
            username = "valid.identity",
            name = "Valid Identity",
            email = "valid.identity@example.com",
            password = "StrongPassword123!"
        };
}
