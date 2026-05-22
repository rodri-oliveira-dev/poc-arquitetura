using Auth.Api.Contracts.Requests;
using Auth.Api.Contracts.Responses;
using Auth.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Auth.IntegrationTests.Api;

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
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Health_should_return_security_headers()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("nosniff", res.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("DENY", res.Headers.GetValues("X-Frame-Options"));
        Assert.Contains("no-referrer", res.Headers.GetValues("Referrer-Policy"));
        Assert.Contains("none", res.Headers.GetValues("X-Permitted-Cross-Domain-Policies"));
        Assert.Contains("geolocation=(), microphone=(), camera=()", res.Headers.GetValues("Permissions-Policy"));
        Assert.Contains("same-origin", res.Headers.GetValues("Cross-Origin-Opener-Policy"));
        Assert.Contains("same-origin", res.Headers.GetValues("Cross-Origin-Resource-Policy"));
        Assert.Contains("default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'", res.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task Unknown_route_should_return_problem_details()
    {
        var res = await _client.GetAsync("/rota-inexistente");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Swagger_should_be_disabled_by_default_in_test()
    {
        var res = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Jwks_should_return_key_with_kid()
    {
        var res = await _client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("keys").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("keys")[0].GetProperty("kid").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("keys")[0].GetProperty("n").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("keys")[0].GetProperty("e").GetString()));
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
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(600, body.ExpiresIn);
        Assert.Equal("ledger.write", body.Scope);
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
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_scope", body!.Error);
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
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
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
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/login", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/login", request)).StatusCode);
        var rejected = await client.PostAsJsonAsync("/auth/login", request);
        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }
}
