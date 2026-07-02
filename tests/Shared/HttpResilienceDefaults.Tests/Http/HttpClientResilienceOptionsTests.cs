using System.Net;

using HttpResilienceDefaults.Tests.Support;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Polly.CircuitBreaker;

namespace HttpResilienceDefaults.Tests.Http;

public sealed class HttpClientResilienceOptionsTests
{
    [Fact]
    public void Defaults_should_be_valid()
    {
        HttpClientResilienceOptions options = new();

        options.Validate("SharedClient");
    }

    [Fact]
    public void Validate_should_reject_zero_total_timeout()
    {
        HttpClientResilienceOptions options = new()
        {
            TotalTimeout = TimeSpan.Zero
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("SharedClient"));

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

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("SharedClient"));

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

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("SharedClient"));

        Assert.Contains("CircuitBreakerFailureRatio", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_should_reject_zero_retry_count()
    {
        HttpClientResilienceOptions options = new()
        {
            RetryCount = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate("SharedClient"));

        Assert.Contains("RetryCount", exception.Message, StringComparison.Ordinal);
        Assert.Contains("maior que zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resilience_handler_should_retry_transient_http_failures_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "dependency unavailable");
        handler.Enqueue(HttpStatusCode.OK, "ok");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
            ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "10"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_retry_transient_post_failures_when_unsafe_methods_are_enabled_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "dependency unavailable");
        handler.Enqueue(HttpStatusCode.OK, "ok");

        using var provider = CreateProvider("UnsafeClient", handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:UnsafeClient:RetryCount"] = "1",
            ["HttpResilience:Clients:UnsafeClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:UnsafeClient:RetryUnsafeHttpMethods"] = "true",
            ["HttpResilience:Clients:UnsafeClient:CircuitBreakerMinimumThroughput"] = "10"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("UnsafeClient");
        using var content = new FormUrlEncodedContent([]);

        using var response = await client.PostAsync("operations", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_retry_http_request_exception_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        handler.Enqueue(HttpStatusCode.OK, "ok");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
            ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "10"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Resilience_handler_should_not_retry_expected_business_failures_Async(HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(statusCode, "expected error");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:SharedClient:RetryCount"] = "3",
            ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using var response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_not_retry_expected_post_failures_when_unsafe_methods_are_enabled_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "invalid credentials");

        using var provider = CreateProvider("UnsafeClient", handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:UnsafeClient:RetryCount"] = "3",
            ["HttpResilience:Clients:UnsafeClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:UnsafeClient:RetryUnsafeHttpMethods"] = "true"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("UnsafeClient");
        using var content = new FormUrlEncodedContent([]);

        using var response = await client.PostAsync("operations", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_open_circuit_and_recover_after_break_duration_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "failure 1");
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "failure 2");
        handler.Enqueue(HttpStatusCode.OK, "recovered");

        using var provider = CreateProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
            ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerFailureRatio"] = "0.5",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "2",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerSamplingDuration"] = "00:00:05",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerBreakDuration"] = "00:00:00.500"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using var first = await client.GetAsync("health", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => client.GetAsync("health", TestContext.Current.CancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        using var recovered = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Resilience_handler_should_not_open_circuit_for_expected_post_failures_when_unsafe_methods_are_enabled_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "invalid credentials");
        handler.Enqueue(HttpStatusCode.Unauthorized, "invalid credentials");
        handler.Enqueue(HttpStatusCode.OK, "ok");

        using var provider = CreateProvider("UnsafeClient", handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:UnsafeClient:RetryCount"] = "1",
            ["HttpResilience:Clients:UnsafeClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:UnsafeClient:RetryUnsafeHttpMethods"] = "true",
            ["HttpResilience:Clients:UnsafeClient:CircuitBreakerFailureRatio"] = "0.5",
            ["HttpResilience:Clients:UnsafeClient:CircuitBreakerMinimumThroughput"] = "2",
            ["HttpResilience:Clients:UnsafeClient:CircuitBreakerSamplingDuration"] = "00:00:05",
            ["HttpResilience:Clients:UnsafeClient:CircuitBreakerBreakDuration"] = "00:00:05"
        });

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("UnsafeClient");
        using var firstContent = new FormUrlEncodedContent([]);
        using var secondContent = new FormUrlEncodedContent([]);
        using var thirdContent = new FormUrlEncodedContent([]);

        using var first = await client.PostAsync("operations", firstContent, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync("operations", secondContent, TestContext.Current.CancellationToken);
        using var third = await client.PostAsync("operations", thirdContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(3, handler.RequestCount);
    }

    private static ServiceProvider CreateProvider(FakeHttpMessageHandler handler, Dictionary<string, string?> overrides)
        => CreateProvider("SharedClient", handler, overrides);

    private static ServiceProvider CreateProvider(
        string clientName,
        FakeHttpMessageHandler handler,
        Dictionary<string, string?> overrides)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"HttpResilience:Clients:{clientName}:TotalTimeout"] = "00:00:05",
                [$"HttpResilience:Clients:{clientName}:AttemptTimeout"] = "00:00:01",
                [$"HttpResilience:Clients:{clientName}:RetryCount"] = "1",
                [$"HttpResilience:Clients:{clientName}:RetryDelay"] = "00:00:00.001",
                [$"HttpResilience:Clients:{clientName}:CircuitBreakerFailureRatio"] = "1",
                [$"HttpResilience:Clients:{clientName}:CircuitBreakerMinimumThroughput"] = "100",
                [$"HttpResilience:Clients:{clientName}:CircuitBreakerSamplingDuration"] = "00:00:30",
                [$"HttpResilience:Clients:{clientName}:CircuitBreakerBreakDuration"] = "00:00:05"
            })
            .AddInMemoryCollection(overrides)
            .Build();

        ServiceCollection services = [];
        services.AddLogging();
        services
            .AddHttpClient(clientName, client => client.BaseAddress = new Uri($"https://{clientName}.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience(configuration, clientName);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
