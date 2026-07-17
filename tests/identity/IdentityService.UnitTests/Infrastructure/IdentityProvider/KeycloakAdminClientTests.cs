using System.Net;
using System.Text;

using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Users.Ports;
using IdentityService.Infrastructure.IdentityProvider;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.UnitTests.Infrastructure.IdentityProvider;

public sealed class KeycloakAdminClientTests
{
    [Fact]
    public async Task CreateUserAsync_should_create_user_set_password_and_return_keycloak_user_id_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.Created, string.Empty, "/admin/realms/poc/users/keycloak-user-1");
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        var result = await fixture.Client.CreateUserAsync(
            new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
            CancellationToken.None);

        Assert.Equal("keycloak-user-1", result.KeycloakUserId);
        Assert.Equal(3, fixture.Handler.Requests.Count);
        Assert.Equal("/realms/poc/protocol/openid-connect/token", fixture.Handler.Requests[0].RequestUri?.PathAndQuery);
        Assert.Contains("grant_type=client_credentials", fixture.Handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("client_id=identity-service-admin", fixture.Handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("client_secret=admin-secret", fixture.Handler.RequestBodies[0], StringComparison.Ordinal);

        Assert.Equal(HttpMethod.Post, fixture.Handler.Requests[1].Method);
        Assert.Equal("/admin/realms/poc/users", fixture.Handler.Requests[1].RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", fixture.Handler.Requests[1].Headers.Authorization?.Scheme);
        Assert.Equal("admin-token", fixture.Handler.Requests[1].Headers.Authorization?.Parameter);
        Assert.Contains("\"firstName\":\"User Name\"", fixture.Handler.RequestBodies[1], StringComparison.Ordinal);
        Assert.Contains("\"username\":\"user-name\"", fixture.Handler.RequestBodies[1], StringComparison.Ordinal);
        Assert.Contains("\"email\":\"user@example.com\"", fixture.Handler.RequestBodies[1], StringComparison.Ordinal);

        Assert.Equal(HttpMethod.Put, fixture.Handler.Requests[2].Method);
        Assert.Equal("/admin/realms/poc/users/keycloak-user-1/reset-password", fixture.Handler.Requests[2].RequestUri?.PathAndQuery);
        Assert.Contains("\"type\":\"password\"", fixture.Handler.RequestBodies[2], StringComparison.Ordinal);
        Assert.Contains("N3ver-log-me!", fixture.Handler.RequestBodies[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_translate_keycloak_failure_to_application_error_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.InternalServerError, "internal provider details");

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Unexpected, exception.Kind);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("Keycloak retornou 500", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("internal provider details", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, IdentityProviderErrorKind.Conflict)]
    [InlineData(HttpStatusCode.Unauthorized, IdentityProviderErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, IdentityProviderErrorKind.Unauthorized)]
    public async Task CreateUserAsync_should_classify_expected_admin_errors_Async(
        HttpStatusCode statusCode,
        IdentityProviderErrorKind expectedKind)
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(statusCode, "provider response");

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(expectedKind, exception.Kind);
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task CreateUserAsync_should_not_expose_password_in_public_exception_or_logs_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        const string password = "N3ver-log-me!";
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.Created, string.Empty, "/admin/realms/poc/users/keycloak-user-1");
        fixture.Handler.Enqueue(HttpStatusCode.BadRequest, $"provider body contains {password}");
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", password),
                CancellationToken.None));

        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, string.Join(Environment.NewLine, fixture.Logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_delete_keycloak_user_when_password_reset_fails_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.Created, string.Empty, "/admin/realms/poc/users/keycloak-user-1");
        fixture.Handler.Enqueue(HttpStatusCode.BadRequest, "invalid password");
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Unexpected, exception.Kind);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(4, fixture.Handler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, fixture.Handler.Requests[3].Method);
        Assert.Equal("/admin/realms/poc/users/keycloak-user-1", fixture.Handler.Requests[3].RequestUri?.PathAndQuery);
        Assert.Contains("Compensando usuario criado no Keycloak", fixture.Logger.JoinedMessages, StringComparison.Ordinal);
        Assert.Contains("Usuario criado no Keycloak removido", fixture.Logger.JoinedMessages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_delete_keycloak_user_when_request_is_canceled_after_user_creation_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        using var cts = new CancellationTokenSource();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(
            HttpStatusCode.Created,
            string.Empty,
            "/admin/realms/poc/users/keycloak-user-1",
            onSend: () => cts.Cancel());
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                cts.Token));

        Assert.Equal(3, fixture.Handler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, fixture.Handler.Requests[2].Method);
        Assert.Equal("/admin/realms/poc/users/keycloak-user-1", fixture.Handler.Requests[2].RequestUri?.PathAndQuery);
        Assert.Contains("Compensando usuario criado no Keycloak", fixture.Logger.JoinedMessages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_preserve_original_exception_when_compensation_fails_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.Created, string.Empty, "/admin/realms/poc/users/keycloak-user-1");
        fixture.Handler.Enqueue(HttpStatusCode.BadRequest, "invalid password");
        fixture.Handler.Enqueue(HttpStatusCode.InternalServerError, "delete failed");

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Unexpected, exception.Kind);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("definir senha no Keycloak", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("remover usuario no Keycloak", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Falha ao compensar usuario criado no Keycloak", fixture.Logger.JoinedMessages, StringComparison.Ordinal);
        Assert.Contains("OriginalExceptionType: IdentityProviderException", fixture.Logger.JoinedMessages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_translate_timeout_to_application_error_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueFailure(new TaskCanceledException("simulated timeout"));

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Timeout, exception.Kind);
        Assert.Contains("Timeout", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "http://keycloak.local",
        "/realms/poc/protocol/openid-connect/token",
        "http://keycloak.local/realms/poc/protocol/openid-connect/token")]
    [InlineData(
        "http://keycloak.local/",
        "/realms/poc/protocol/openid-connect/token",
        "http://keycloak.local/realms/poc/protocol/openid-connect/token")]
    [InlineData(
        "http://keycloak.local",
        "realms/poc/protocol/openid-connect/token",
        "http://keycloak.local/realms/poc/protocol/openid-connect/token")]
    [InlineData(
        "http://keycloak.local/",
        "realms/poc/protocol/openid-connect/token",
        "http://keycloak.local/realms/poc/protocol/openid-connect/token")]
    [InlineData(
        "http://keycloak.local",
        "http://identity.example/realms/poc/protocol/openid-connect/token",
        "http://identity.example/realms/poc/protocol/openid-connect/token")]
    [InlineData(
        "http://keycloak.local",
        "https://identity.example/realms/poc/protocol/openid-connect/token",
        "https://identity.example/realms/poc/protocol/openid-connect/token")]
    public async Task CreateUserAsync_should_build_token_uri_from_supported_endpoint_formats_Async(
        string baseUrl,
        string tokenEndpoint,
        string expectedTokenUri)
    {
        using var fixture = new KeycloakAdminClientFixture(baseUrl, tokenEndpoint);
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.Created, string.Empty, "/admin/realms/poc/users/keycloak-user-1");
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        await fixture.Client.CreateUserAsync(
            new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
            CancellationToken.None);

        Assert.Equal(expectedTokenUri, fixture.Handler.Requests[0].RequestUri?.AbsoluteUri);
        Assert.NotEqual(Uri.UriSchemeFile, fixture.Handler.Requests[0].RequestUri?.Scheme);
    }

    [Fact]
    public async Task CreateUserAsync_should_reject_absolute_token_endpoint_with_non_http_scheme_Async()
    {
        using var fixture = new KeycloakAdminClientFixture(tokenEndpoint: "ftp://identity.example/token");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Contains("HTTP/HTTPS", exception.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CreateUserAsync_should_classify_token_endpoint_auth_errors_as_unauthorized_Async(
        HttpStatusCode statusCode)
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.Enqueue(statusCode, "provider response");

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Unauthorized, exception.Kind);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.DoesNotContain("N3ver-log-me!", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_propagate_cancellation_without_translating_to_timeout_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("User Name", "user@example.com", "user-name", "N3ver-log-me!"),
                cts.Token));

        Assert.Empty(fixture.Logger.Messages);
    }

    [Fact]
    public async Task DeleteUserAsync_should_call_keycloak_delete_for_compensation_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.NoContent, string.Empty);

        await fixture.Client.DeleteUserAsync("keycloak-user-1", CancellationToken.None);

        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, fixture.Handler.Requests[1].Method);
        Assert.Equal("/admin/realms/poc/users/keycloak-user-1", fixture.Handler.Requests[1].RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task DeleteUserAsync_should_treat_missing_keycloak_user_as_success_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "admin-token" }""");
        fixture.Handler.Enqueue(HttpStatusCode.NotFound, "already removed");

        await fixture.Client.DeleteUserAsync("keycloak-user-1", CancellationToken.None);

        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, fixture.Handler.Requests[1].Method);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteUserAsync_should_reject_empty_keycloak_user_id_Async(string keycloakUserId)
    {
        using var fixture = new KeycloakAdminClientFixture();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Client.DeleteUserAsync(keycloakUserId, CancellationToken.None));

        Assert.Equal("keycloakUserId", exception.ParamName);
        Assert.Empty(fixture.Handler.Requests);
    }

    private sealed class KeycloakAdminClientFixture : IDisposable
    {
        private readonly HttpClient _httpClient;

        public KeycloakAdminClientFixture(
            string baseUrl = "http://keycloak.local",
            string tokenEndpoint = "/realms/poc/protocol/openid-connect/token")
        {
            Handler = new FakeHttpMessageHandler();
            Logger = new CapturingLogger<KeycloakAdminClient>();
            _httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute)
            };

            Client = new KeycloakAdminClient(
                _httpClient,
                new StaticOptions<KeycloakAdminOptions>(new KeycloakAdminOptions
                {
                    BaseUrl = baseUrl,
                    Realm = "poc",
                    TokenEndpoint = tokenEndpoint,
                    ClientId = "identity-service-admin",
                    ClientSecret = "admin-secret",
                    Timeout = TimeSpan.FromSeconds(10),
                    CompensationTimeout = TimeSpan.FromSeconds(1)
                }),
                TimeProvider.System,
                Logger);
        }

        public FakeHttpMessageHandler Handler
        {
            get;
        }

        public CapturingLogger<KeycloakAdminClient> Logger
        {
            get;
        }

        public KeycloakAdminClient Client
        {
            get;
        }

        public void Dispose()
            => _httpClient.Dispose();
    }

    private sealed class StaticOptions<TOptions>(TOptions value) : IOptions<TOptions>
        where TOptions : class
    {
        public TOptions Value
        {
            get;
        } = value;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

        public List<HttpRequestMessage> Requests
        {
            get;
        } = [];

        public List<string> RequestBodies
        {
            get;
        } = [];

        public void EnqueueJson(HttpStatusCode statusCode, string json)
            => Enqueue(statusCode, json, contentType: "application/json");

        public void Enqueue(
            HttpStatusCode statusCode,
            string content,
            string? location = null,
            string contentType = "text/plain",
            Action? onSend = null)
        {
            _responses.Enqueue((_, _) =>
            {
                onSend?.Invoke();
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, contentType)
                };

                if (location is not null)
                    response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);

                return Task.FromResult(response);
            });
        }

        public void EnqueueFailure(Exception exception)
            => _responses.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(exception));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            var response = _responses.Count == 0
                ? throw new InvalidOperationException("No fake HTTP response was configured.")
                : _responses.Dequeue();

            return await response(request, cancellationToken);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages
        {
            get;
        } = [];

        public string JoinedMessages
            => string.Join(Environment.NewLine, Messages);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));

            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
