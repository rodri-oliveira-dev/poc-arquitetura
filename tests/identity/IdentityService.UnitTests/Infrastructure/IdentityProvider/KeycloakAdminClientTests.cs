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
            new CreateIdentityProviderUserRequest("user@example.com", "user-name", "N3ver-log-me!"),
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
                new CreateIdentityProviderUserRequest("user@example.com", "user-name", "N3ver-log-me!"),
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
                new CreateIdentityProviderUserRequest("user@example.com", "user-name", "N3ver-log-me!"),
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

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("user@example.com", "user-name", password),
                CancellationToken.None));

        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, string.Join(Environment.NewLine, fixture.Logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_should_translate_timeout_to_application_error_Async()
    {
        using var fixture = new KeycloakAdminClientFixture();
        fixture.Handler.EnqueueFailure(new TaskCanceledException("simulated timeout"));

        var exception = await Assert.ThrowsAsync<IdentityProviderException>(() =>
            fixture.Client.CreateUserAsync(
                new CreateIdentityProviderUserRequest("user@example.com", "user-name", "N3ver-log-me!"),
                CancellationToken.None));

        Assert.Equal(IdentityProviderErrorKind.Timeout, exception.Kind);
        Assert.Contains("Timeout", exception.Message, StringComparison.Ordinal);
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

    private sealed class KeycloakAdminClientFixture : IDisposable
    {
        private readonly HttpClient _httpClient;

        public KeycloakAdminClientFixture()
        {
            Handler = new FakeHttpMessageHandler();
            Logger = new CapturingLogger<KeycloakAdminClient>();
            _httpClient = new HttpClient(Handler)
            {
                BaseAddress = new Uri("http://keycloak.local")
            };

            Client = new KeycloakAdminClient(
                _httpClient,
                new StaticOptions<KeycloakAdminOptions>(new KeycloakAdminOptions
                {
                    BaseUrl = "http://keycloak.local",
                    Realm = "poc",
                    TokenEndpoint = "/realms/poc/protocol/openid-connect/token",
                    ClientId = "identity-service-admin",
                    ClientSecret = "admin-secret",
                    Timeout = TimeSpan.FromSeconds(10)
                }),
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

        public void Enqueue(HttpStatusCode statusCode, string content, string? location = null, string contentType = "text/plain")
        {
            _responses.Enqueue((_, _) =>
            {
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
