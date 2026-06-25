using HttpResilienceDefaults;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Polly.CircuitBreaker;

using TransferService.Worker.Tests.Support;

namespace TransferService.Worker.Tests.Http;

public sealed class HttpClientResilienceOptionsTests
{
    [Fact]
    public void Defaults_should_be_valid()
    {
        HttpClientResilienceOptions options = new();

        options.Validate("Ledger");
    }

    [Fact]
    public void Validate_should_reject_zero_total_timeout()
    {
        HttpClientResilienceOptions options = new()
        {
            TotalTimeout = TimeSpan.Zero
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("Ledger"));

        Assert.Contains("TotalTimeout", exception.Message, StringComparison.Ordinal);
        Assert.Contains("maior que zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_attempt_timeout_greater_than_total_timeout()
    {
        HttpClientResilienceOptions options = new()
        {
            TotalTimeout = TimeSpan.FromSeconds(1),
            AttemptTimeout = TimeSpan.FromSeconds(2)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("JWKS"));

        Assert.Contains("AttemptTimeout", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TotalTimeout", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_invalid_circuit_breaker_failure_ratio()
    {
        HttpClientResilienceOptions options = new()
        {
            CircuitBreakerFailureRatio = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("Keycloak"));

        Assert.Contains("CircuitBreakerFailureRatio", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_zero_retry_count()
    {
        HttpClientResilienceOptions options = new()
        {
            RetryCount = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("Ledger"));

        Assert.Contains("RetryCount", exception.Message, StringComparison.Ordinal);
        Assert.Contains("maior que zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resilience_handler_should_retry_transient_http_failures_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(System.Net.HttpStatusCode.ServiceUnavailable, "ledger indisponivel");
        handler.Enqueue(System.Net.HttpStatusCode.OK, "ok");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Ledger:RetryCount"] = "1",
            ["HttpResilience:Clients:Ledger:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:Ledger:CircuitBreakerMinimumThroughput"] = "10"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Ledger");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_retry_http_request_exception_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        handler.Enqueue(System.Net.HttpStatusCode.OK, "ok");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Ledger:RetryCount"] = "1",
            ["HttpResilience:Clients:Ledger:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:Ledger:CircuitBreakerMinimumThroughput"] = "10"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Ledger");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.BadRequest)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    public async Task Resilience_handler_should_not_retry_expected_business_failures_Async(System.Net.HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(statusCode, "erro esperado");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Ledger:RetryCount"] = "3",
            ["HttpResilience:Clients:Ledger:RetryDelay"] = "00:00:00.001"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Ledger");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_open_circuit_and_recover_after_break_duration_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(System.Net.HttpStatusCode.ServiceUnavailable, "falha 1");
        handler.Enqueue(System.Net.HttpStatusCode.ServiceUnavailable, "falha 2");
        handler.Enqueue(System.Net.HttpStatusCode.OK, "recuperado");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Ledger:RetryCount"] = "1",
            ["HttpResilience:Clients:Ledger:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:Ledger:CircuitBreakerFailureRatio"] = "0.5",
            ["HttpResilience:Clients:Ledger:CircuitBreakerMinimumThroughput"] = "2",
            ["HttpResilience:Clients:Ledger:CircuitBreakerSamplingDuration"] = "00:00:05",
            ["HttpResilience:Clients:Ledger:CircuitBreakerBreakDuration"] = "00:00:00.500"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Ledger");

        using var first = await client.GetAsync("health", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => client.GetAsync("health", TestContext.Current.CancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        using var recovered = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(3, handler.RequestCount);
    }

    private static ServiceProvider CreateProvider(FakeHttpMessageHandler handler, Dictionary<string, string?> overrides)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpResilience:Clients:Ledger:TotalTimeout"] = "00:00:05",
                ["HttpResilience:Clients:Ledger:AttemptTimeout"] = "00:00:01",
                ["HttpResilience:Clients:Ledger:RetryCount"] = "1",
                ["HttpResilience:Clients:Ledger:RetryDelay"] = "00:00:00.001",
                ["HttpResilience:Clients:Ledger:CircuitBreakerFailureRatio"] = "1",
                ["HttpResilience:Clients:Ledger:CircuitBreakerMinimumThroughput"] = "100",
                ["HttpResilience:Clients:Ledger:CircuitBreakerSamplingDuration"] = "00:00:30",
                ["HttpResilience:Clients:Ledger:CircuitBreakerBreakDuration"] = "00:00:05"
            })
            .AddInMemoryCollection(overrides)
            .Build();

        ServiceCollection services = [];
        services.AddLogging();
        services
            .AddHttpClient("Ledger", client => client.BaseAddress = new Uri("https://ledger.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience(configuration, "Ledger");

        return services.BuildServiceProvider(validateScopes: true);
    }
}
