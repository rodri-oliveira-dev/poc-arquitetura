using System.Net;

using HttpResilienceDefaults;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;

using TransferService.Worker.Ledger;
using TransferService.Worker.Options;
using TransferService.Worker.Tests.Support;

namespace TransferService.Worker.Tests.Http;

public sealed class HttpClientCircuitBreakerPolicyTests
{
    [Fact]
    public async Task Ledger_client_should_execute_call_when_dependency_is_healthy_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var lancamentoId = Guid.NewGuid();
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{lancamentoId}}" }""");
        using ServiceProvider provider = CreateLedgerProvider(handler);

        var client = provider.GetRequiredService<ILedgerServiceClient>();

        var result = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-1", "corr-1", TestContext.Current.CancellationToken);

        Assert.Equal(lancamentoId, result.LancamentoId);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Ledger_client_should_retry_transient_failure_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var lancamentoId = Guid.NewGuid();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "ledger indisponivel");
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{lancamentoId}}" }""");
        using ServiceProvider provider = CreateLedgerProvider(handler);

        var client = provider.GetRequiredService<ILedgerServiceClient>();

        var result = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-1", null, TestContext.Current.CancellationToken);

        Assert.Equal(lancamentoId, result.LancamentoId);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Ledger_client_should_open_circuit_fail_fast_and_close_after_successful_recovery_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var recoveredLancamentoId = Guid.NewGuid();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "falha 1");
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "falha 2");
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{recoveredLancamentoId}}" }""");
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{Guid.NewGuid()}}" }""");
        using ServiceProvider provider = CreateLedgerProvider(handler, CreateFastCircuitBreakerOverrides("Ledger"));

        var client = provider.GetRequiredService<ILedgerServiceClient>();

        await Assert.ThrowsAsync<LedgerServiceException>(
            () => client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-1", null, TestContext.Current.CancellationToken));
        int requestsBeforeOpenCircuit = handler.RequestCount;

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-2", null, TestContext.Current.CancellationToken));
        int requestsAfterFastFailure = handler.RequestCount;

        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        var recovered = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-3", null, TestContext.Current.CancellationToken);
        var closedCircuitCall = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-4", null, TestContext.Current.CancellationToken);

        Assert.Equal(2, requestsBeforeOpenCircuit);
        Assert.Equal(requestsBeforeOpenCircuit, requestsAfterFastFailure);
        Assert.Equal(recoveredLancamentoId, recovered.LancamentoId);
        Assert.NotEqual(Guid.Empty, closedCircuitCall.LancamentoId);
        Assert.Equal(4, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Ledger_client_should_not_retry_or_open_circuit_for_business_errors_Async(HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(statusCode, "erro de negocio");
        handler.Enqueue(statusCode, "erro de negocio");
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{Guid.NewGuid()}}" }""");
        using ServiceProvider provider = CreateLedgerProvider(handler, CreateFastCircuitBreakerOverrides("Ledger"));

        var client = provider.GetRequiredService<ILedgerServiceClient>();

        var first = await Assert.ThrowsAsync<LedgerServiceException>(
            () => client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-1", null, TestContext.Current.CancellationToken));
        var second = await Assert.ThrowsAsync<LedgerServiceException>(
            () => client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-2", null, TestContext.Current.CancellationToken));
        var afterBusinessErrors = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-3", null, TestContext.Current.CancellationToken);

        Assert.Equal(statusCode, first.StatusCode);
        Assert.Equal(statusCode, second.StatusCode);
        Assert.NotEqual(Guid.Empty, afterBusinessErrors.LancamentoId);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Ledger_client_should_treat_timeout_as_transient_failure_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var lancamentoId = Guid.NewGuid();
        handler.EnqueueDelay(TimeSpan.FromMilliseconds(200), HttpStatusCode.Created, "late");
        handler.EnqueueJson(HttpStatusCode.Created, $$"""{ "lancamentoId": "{{lancamentoId}}" }""");
        using ServiceProvider provider = CreateLedgerProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Ledger:AttemptTimeout"] = "00:00:00.050",
            ["HttpResilience:Clients:Ledger:TotalTimeout"] = "00:00:01"
        });

        var client = provider.GetRequiredService<ILedgerServiceClient>();

        var result = await client.CreateLancamentoAsync(CreateLedgerRequest(), "idem-1", null, TestContext.Current.CancellationToken);

        Assert.Equal(lancamentoId, result.LancamentoId);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Keycloak_token_provider_should_execute_call_when_dependency_is_healthy_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-1", "expires_in": 600 }""");
        using ServiceProvider provider = CreateKeycloakProvider(handler);

        var tokenProvider = provider.GetRequiredService<ILedgerAccessTokenProvider>();

        var token = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-1", token);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Keycloak_token_provider_should_retry_transient_failure_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "keycloak indisponivel");
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-2", "expires_in": 600 }""");
        using ServiceProvider provider = CreateKeycloakProvider(handler);

        var tokenProvider = provider.GetRequiredService<ILedgerAccessTokenProvider>();

        var token = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-2", token);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Keycloak_token_provider_should_open_circuit_fail_fast_and_close_after_successful_recovery_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "falha 1");
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "falha 2");
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "recovered-token", "expires_in": 1 }""");
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "closed-token", "expires_in": 600 }""");
        using ServiceProvider provider = CreateKeycloakProvider(handler, CreateFastCircuitBreakerOverrides("Keycloak"));

        var tokenProvider = provider.GetRequiredService<ILedgerAccessTokenProvider>();

        await Assert.ThrowsAsync<LedgerAuthenticationException>(
            () => tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask());
        int requestsBeforeOpenCircuit = handler.RequestCount;

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask());
        int requestsAfterFastFailure = handler.RequestCount;

        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        var recovered = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(1_100), TestContext.Current.CancellationToken);
        var closed = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, requestsBeforeOpenCircuit);
        Assert.Equal(requestsBeforeOpenCircuit, requestsAfterFastFailure);
        Assert.Equal("recovered-token", recovered);
        Assert.Equal("closed-token", closed);
        Assert.Equal(4, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Keycloak_token_provider_should_not_retry_or_open_circuit_for_business_errors_Async(HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.Enqueue(statusCode, "invalid_request");
        handler.Enqueue(statusCode, "invalid_request");
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-3", "expires_in": 600 }""");
        using ServiceProvider provider = CreateKeycloakProvider(handler, CreateFastCircuitBreakerOverrides("Keycloak"));

        var tokenProvider = provider.GetRequiredService<ILedgerAccessTokenProvider>();

        await Assert.ThrowsAsync<LedgerAuthenticationException>(
            () => tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<LedgerAuthenticationException>(
            () => tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask());
        var token = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-3", token);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Keycloak_token_provider_should_treat_timeout_as_transient_failure_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.EnqueueDelay(TimeSpan.FromMilliseconds(200), HttpStatusCode.OK, "late");
        handler.EnqueueJson(HttpStatusCode.OK, /*lang=json,strict*/ """{ "access_token": "token-after-timeout", "expires_in": 600 }""");
        using ServiceProvider provider = CreateKeycloakProvider(handler, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:Keycloak:AttemptTimeout"] = "00:00:00.050",
            ["HttpResilience:Clients:Keycloak:TotalTimeout"] = "00:00:01"
        });

        var tokenProvider = provider.GetRequiredService<ILedgerAccessTokenProvider>();

        var token = await tokenProvider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-after-timeout", token);
        Assert.Equal(2, handler.RequestCount);
    }

    private static ServiceProvider CreateLedgerProvider(
        FakeHttpMessageHandler handler,
        Dictionary<string, string?>? overrides = null)
    {
        IConfiguration configuration = CreateResilienceConfiguration("Ledger", overrides);
        ServiceCollection services = [];
        services.AddLogging();
        services
            .AddHttpClient<ILedgerServiceClient, LedgerServiceClient>(client =>
            {
                client.BaseAddress = new Uri("https://ledger.local/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience(configuration, "Ledger");

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider CreateKeycloakProvider(
        FakeHttpMessageHandler handler,
        Dictionary<string, string?>? overrides = null)
    {
        IConfiguration configuration = CreateResilienceConfiguration("Keycloak", overrides);
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton<IOptionsMonitor<TransferWorkerOptions>>(new StaticOptionsMonitor<TransferWorkerOptions>(CreateWorkerOptions()));
        services.AddSingleton(TimeProvider.System);
        services
            .AddHttpClient<ILedgerAccessTokenProvider, ClientCredentialsLedgerAccessTokenProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience(configuration, "Keycloak");

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration CreateResilienceConfiguration(
        string clientName,
        Dictionary<string, string?>? overrides)
    {
        Dictionary<string, string?> settings = new(StringComparer.OrdinalIgnoreCase)
        {
            [$"HttpResilience:Clients:{clientName}:TotalTimeout"] = "00:00:05",
            [$"HttpResilience:Clients:{clientName}:AttemptTimeout"] = "00:00:01",
            [$"HttpResilience:Clients:{clientName}:RetryCount"] = "1",
            [$"HttpResilience:Clients:{clientName}:RetryDelay"] = "00:00:00.001",
            [$"HttpResilience:Clients:{clientName}:RetryUnsafeHttpMethods"] = "true",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerFailureRatio"] = "1",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerMinimumThroughput"] = "100",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerSamplingDuration"] = "00:00:30",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerBreakDuration"] = "00:00:05"
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string?> option in overrides)
            {
                settings[option.Key] = option.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static Dictionary<string, string?> CreateFastCircuitBreakerOverrides(string clientName)
        => new()
        {
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerFailureRatio"] = "0.5",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerMinimumThroughput"] = "2",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerSamplingDuration"] = "00:00:05",
            [$"HttpResilience:Clients:{clientName}:CircuitBreakerBreakDuration"] = "00:00:00.500"
        };

    private static CreateLedgerLancamentoRequest CreateLedgerRequest()
        => new("merchant-1", "DEBIT", -100m, "Debito transferencia", "transfer-1");

    private static TransferWorkerOptions CreateWorkerOptions()
        => new()
        {
            Ledger =
            {
                Auth =
                {
                    TokenEndpoint = new Uri("https://keycloak.local/realms/poc/protocol/openid-connect/token"),
                    ClientId = "poc-automation",
                    ClientSecret = "local-secret",
                    Scope = "ledger.write",
                    RefreshSkew = TimeSpan.FromMilliseconds(500)
                }
            }
        };

    private sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = value;

        public TOptions Get(string? name)
            => CurrentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener)
            => null;
    }
}
