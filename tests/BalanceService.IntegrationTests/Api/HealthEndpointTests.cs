using BalanceService.IntegrationTests.Infrastructure;

namespace BalanceService.IntegrationTests.Api;

public sealed class HealthEndpointTests : IClassFixture<BalanceApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(BalanceApiFactory factory)
    {
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
}
