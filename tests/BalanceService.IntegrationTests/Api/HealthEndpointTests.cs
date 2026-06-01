using BalanceService.IntegrationTests.Infrastructure;

using Microsoft.Extensions.Configuration;

namespace BalanceService.IntegrationTests.Api;

public sealed class HealthEndpointTests : IClassFixture<BalanceApiFactory>
{
    private readonly BalanceApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(BalanceApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_should_return_200_ok_and_generate_correlation_id()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadAsStringAsync()));
        Assert.True(res.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.True(Guid.TryParse(Assert.Single(values), out _));
    }

    [Fact]
    public async Task Health_should_preserve_explicit_correlation_id()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var res = await _client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(correlationId, Assert.Single(values));
    }

    [Fact]
    public async Task Ready_should_return_200_when_db_is_available()
    {
        var res = await _client.GetAsync("/ready");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ready\"", body);
        Assert.Contains("\"db\":\"ok\"", body);
    }

    [Fact]
    public async Task Swagger_should_be_unavailable_by_default_in_test()
    {
        var res = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Swagger_should_be_enabled_when_explicitly_configured()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Swagger:Enabled"] = "true"
                });
            });
        });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
    }
}
