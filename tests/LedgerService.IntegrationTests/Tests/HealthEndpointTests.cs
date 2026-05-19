using FluentAssertions;
using LedgerService.IntegrationTests.Infrastructure;

namespace LedgerService.IntegrationTests.Tests;

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

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Be("ok");

        // CorrelationId middleware sempre propaga o header
        res.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }

    [Fact]
    public async Task Health_should_preserve_explicit_correlation_id()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var res = await _client.SendAsync(request);

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        res.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Fact]
    public async Task Ready_should_return_200_when_db_is_available()
    {
        var res = await _client.GetAsync("/ready");

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ready\"");
        body.Should().Contain("\"db\":\"ok\"");
        body.Should().NotContain("\"kafka\"");
    }
}
