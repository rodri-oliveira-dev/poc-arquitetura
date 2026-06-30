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
        Assert.Empty(await db.IdempotencyRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Post_users_with_new_idempotency_key_should_return_201()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var response = await PostUserAsync(
            client,
            ValidRequest("idem.new", "idem.new@example.com"),
            "idem-new-1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(factory.IdentityProvider.CreateRequests);
    }

    [Fact]
    public async Task Post_users_retry_with_same_idempotency_key_should_replay_response_without_side_effects()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");
        var request = ValidRequest("idem.replay", "idem.replay@example.com");

        using var firstResponse = await PostUserAsync(client, request, "idem-replay-1");
        using var secondResponse = await PostUserAsync(client, request, "idem-replay-1");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        using var firstDocument = await ReadJsonAsync(firstResponse);
        using var secondDocument = await ReadJsonAsync(secondResponse);
        Assert.Equal(firstDocument.RootElement.GetProperty("id").GetGuid(), secondDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("keycloakUserId").GetString(),
            secondDocument.RootElement.GetProperty("keycloakUserId").GetString());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("merchantId").GetString(),
            secondDocument.RootElement.GetProperty("merchantId").GetString());

        Assert.Single(factory.IdentityProvider.CreateRequests);
        Assert.Single(factory.EmailSender.Messages);

        await using var db = _fixture.CreateDbContext();
        var persistedUsers = await db.Users.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, persistedUsers.Count(user => user.Email.Value == "idem.replay@example.com"));
    }

    [Fact]
    public async Task Post_users_retry_with_same_idempotency_key_and_different_payload_should_return_conflict()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var firstResponse = await PostUserAsync(
            client,
            ValidRequest("idem.conflict", "idem.conflict@example.com"),
            "idem-conflict-1");
        using var secondResponse = await PostUserAsync(
            client,
            ValidRequest("idem.conflict.other", "idem.conflict.other@example.com"),
            "idem-conflict-1");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var problem = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Idempotency key conflict", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("StrongPassword123!", problem, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", problem, StringComparison.Ordinal);
        Assert.Single(factory.IdentityProvider.CreateRequests);
    }

    [Fact]
    public async Task Post_users_should_return_400_for_invalid_idempotency_key()
    {
        using var factory = new PostgresIdentityApiFactory(_fixture.RuntimeConnectionString);
        using var client = CreateAuthenticatedClient(factory, scopes: "identity.write");

        using var response = await PostUserAsync(
            client,
            ValidRequest("idem.invalid", "idem.invalid@example.com"),
            "invalid key with spaces");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Idempotency-Key", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("StrongPassword123!", problem, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", problem, StringComparison.Ordinal);
        Assert.Empty(factory.IdentityProvider.CreateRequests);
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

    private static async Task<HttpResponseMessage> PostUserAsync(
        HttpClient client,
        object request,
        string? idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(request)
        };

        if (idempotencyKey is not null)
            message.Headers.Add("Idempotency-Key", idempotencyKey);

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

    private static object ValidRequest(string username = "valid.identity", string email = "valid.identity@example.com")
        => new
        {
            username,
            name = "Valid Identity",
            email,
            password = "StrongPassword123!"
        };
}
