using BalanceService.IntegrationTests.Infrastructure;
using FluentAssertions;

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

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Be("ok");

        res.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        Guid.TryParse(values.Should().ContainSingle().Which, out _).Should().BeTrue();
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
}
