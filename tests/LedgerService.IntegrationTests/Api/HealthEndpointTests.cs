using LedgerService.IntegrationTests.Infrastructure;

namespace LedgerService.IntegrationTests.Api;

public sealed class HealthEndpointTests : IClassFixture<LedgerApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(LedgerApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_should_return_200_ok()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadAsStringAsync()));
        // CorrelationId middleware sempre propaga o header
        Assert.True(res.Headers.Contains("X-Correlation-Id"));
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
        Assert.DoesNotContain("\"kafka\"", body);
    }
}
