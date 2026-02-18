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
}
