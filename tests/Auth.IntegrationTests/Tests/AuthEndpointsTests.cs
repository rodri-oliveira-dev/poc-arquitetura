using Auth.Api.Contracts;
using Auth.IntegrationTests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Auth.IntegrationTests.Tests;

public sealed class AuthEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_should_return_ok()
    {
        var res = await _client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Be("ok");
    }

    [Fact]
    public async Task Health_should_return_security_headers()
    {
        var res = await _client.GetAsync("/health");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        res.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        res.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        res.Headers.GetValues("X-Permitted-Cross-Domain-Policies").Should().Contain("none");
        res.Headers.GetValues("Permissions-Policy").Should().Contain("geolocation=(), microphone=(), camera=()");
        res.Headers.GetValues("Cross-Origin-Opener-Policy").Should().Contain("same-origin");
        res.Headers.GetValues("Cross-Origin-Resource-Policy").Should().Contain("same-origin");
        res.Headers.GetValues("Content-Security-Policy").Should().Contain("default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'");
    }

    [Fact]
    public async Task Unknown_route_should_return_problem_details()
    {
        var res = await _client.GetAsync("/rota-inexistente");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        res.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        doc.RootElement.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Swagger_should_be_disabled_by_default_in_test()
    {
        var res = await _client.GetAsync("/swagger/v1/swagger.json");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Swagger_should_be_enabled_when_explicitly_configured()
    {
        using var factory = AuthApiFactory.WithConfigurationOverrides(new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/swagger/v1/swagger.json");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jwks_should_return_key_with_kid()
    {
        var res = await _client.GetAsync("/.well-known/jwks.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("keys").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("keys")[0].GetProperty("kid").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("keys")[0].GetProperty("n").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("keys")[0].GetProperty("e").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_should_return_token_for_valid_credentials()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "poc-usuario",
            Password = "Poc#123",
            Scope = "ledger.write"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.TokenType.Should().Be("Bearer");
        body.ExpiresIn.Should().Be(600);
        body.Scope.Should().Be("ledger.write");
    }

    [Fact]
    public async Task Login_should_return_400_when_scope_is_empty()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "poc-usuario",
            Password = "Poc#123",
            Scope = string.Empty
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        body.Should().NotBeNull();
        body!.Error.Should().Be("invalid_scope");
    }

    [Fact]
    public async Task Login_should_return_401_for_invalid_credentials()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "wrong",
            Password = "wrong",
            Scope = "ledger.write"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_should_return_400_for_invalid_scope()
    {
        var res = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "poc-usuario",
            Password = "Poc#123",
            Scope = "invalid.scope"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_should_return_429_when_rate_limit_is_exceeded()
    {
        using var factory = AuthApiFactory.WithConfigurationOverrides(new Dictionary<string, string?>
        {
            ["Auth:LoginRateLimit:PermitLimit"] = "2",
            ["Auth:LoginRateLimit:WindowSeconds"] = "60"
        });

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var request = new LoginRequest
        {
            Username = "poc-usuario",
            Password = "Poc#123",
            Scope = "ledger.write"
        };

        (await client.PostAsJsonAsync("/auth/login", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/auth/login", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await client.PostAsJsonAsync("/auth/login", request);

        rejected.StatusCode.Should().Be((HttpStatusCode)429);
        rejected.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
