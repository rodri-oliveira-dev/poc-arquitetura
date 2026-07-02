using System.Diagnostics.Metrics;
using System.Net;

using HttpResilienceDefaults.Tests.Support;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Polly.CircuitBreaker;

namespace HttpResilienceDefaults.Tests.Http;

public sealed class HttpResilienceMetricsTests
{
    private static readonly string[] ProhibitedTags =
    [
        "correlation_id",
        "trace_id",
        "span_id",
        "event_id",
        "outbox_message_id",
        "merchant_id",
        "payload",
        "url",
        "path"
    ];

    [Fact]
    public void Metric_names_should_preserve_dashboard_compatibility()
    {
        Assert.Equal("HttpResilienceDefaults", HttpResilienceMetrics.MeterName);
    }

    [Fact]
    public void RecordRetry_should_emit_low_cardinality_tags()
    {
        var meterName = $"{HttpResilienceMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var listener = new MeterListener();
        using var metrics = new HttpResilienceMetrics(meterName);

        ObservedMetric? observed = null;
        EnableMetric(listener, meterName, "http.resilience.retries");
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            observed = new ObservedMetric(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        });

        listener.Start();

        metrics.RecordRetry("SharedClient", "POST", new HttpRequestException("connection refused"));

        Assert.NotNull(observed);
        Assert.Equal(1L, observed.Measurement);
        AssertTags(
            observed.Tags,
            "SharedClient",
            "SharedClient",
            "POST",
            "retry",
            "HttpRequestException");
    }

    [Fact]
    public async Task Resilience_handler_should_emit_retry_timeout_and_duration_metrics_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var meterName = $"{HttpResilienceMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        handler.EnqueueDelay(TimeSpan.FromSeconds(30), HttpStatusCode.OK, "late");
        handler.Enqueue(HttpStatusCode.OK, "ok");

        using var listener = new MeterListener();
        var observed = new List<ObservedMetric>();
        EnableAllHttpResilienceMetrics(listener, observed, meterName);
        listener.Start();

        using ServiceProvider provider = CreateProvider(handler, meterName, new Dictionary<string, string?>
        {
            ["HttpResilience:Clients:SharedClient:AttemptTimeout"] = "00:00:00.050",
            ["HttpResilience:Clients:SharedClient:TotalTimeout"] = "00:00:02",
            ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
            ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
            ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "10"
        });
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using HttpResponseMessage response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertObserved(observed, "http.resilience.retries", "retry", "TimeoutRejectedException");
        AssertObserved(observed, "http.resilience.timeouts", "timeout", "TimeoutRejectedException");
        AssertObserved(observed, "http.resilience.request.duration", "success");
        Assert.Contains(observed, metric =>
            metric.Name == "http.resilience.request.duration" &&
            metric.Measurement is double duration &&
            duration >= 0);
    }

    [Fact]
    public async Task Resilience_handler_should_emit_circuit_breaker_transition_and_rejection_metrics_Async()
    {
        using var handler = new FakeHttpMessageHandler();
        var meterName = $"{HttpResilienceMetrics.MeterName}.Tests.{Guid.NewGuid():N}";
        using var loggerProvider = new InMemoryLoggerProvider();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "failure 1");
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "failure 2");
        handler.Enqueue(HttpStatusCode.OK, "recovered");
        handler.Enqueue(HttpStatusCode.OK, "closed");

        using var listener = new MeterListener();
        var observed = new List<ObservedMetric>();
        EnableAllHttpResilienceMetrics(listener, observed, meterName);
        listener.Start();

        using ServiceProvider provider = CreateProvider(
            handler,
            meterName,
            new Dictionary<string, string?>
            {
                ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
                ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerFailureRatio"] = "0.5",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "2",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerSamplingDuration"] = "00:00:05",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerBreakDuration"] = "00:00:00.500"
            },
            loggerProvider);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SharedClient");

        using HttpResponseMessage first = await client.GetAsync("health", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => client.GetAsync("health", TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        using HttpResponseMessage recovered = await client.GetAsync("health", TestContext.Current.CancellationToken);
        using HttpResponseMessage closed = await client.GetAsync("health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        AssertObserved(observed, "http.resilience.retries", "retry");
        AssertObserved(observed, "http.resilience.circuit_breaker.opened", "open");
        AssertObserved(observed, "http.resilience.circuit_breaker.half_opened", "half_open");
        AssertObserved(observed, "http.resilience.circuit_breaker.closed", "closed");
        AssertObserved(observed, "http.resilience.open_circuit.rejected_calls", "open_circuit_rejected", "BrokenCircuitException");
        AssertObserved(observed, "http.resilience.request.duration", "open_circuit_rejected", "BrokenCircuitException");
        Assert.Contains(observed, metric =>
            metric.Name == "http.resilience.request.duration" &&
            metric.Measurement is double duration &&
            duration >= 0);

        Assert.Contains(loggerProvider.Messages, message => message.Contains("Retry HTTP resiliente agendado", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Circuit breaker HTTP aberto", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Tentativa HTTP em half-open", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Circuit breaker HTTP fechado", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Chamada HTTP rejeitada por circuito aberto", StringComparison.Ordinal));
        Assert.DoesNotContain(loggerProvider.Messages, message => message.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(loggerProvider.Messages, message => message.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceProvider CreateProvider(
        FakeHttpMessageHandler handler,
        string meterName,
        Dictionary<string, string?> overrides,
        ILoggerProvider? loggerProvider = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpResilience:Clients:SharedClient:TotalTimeout"] = "00:00:05",
                ["HttpResilience:Clients:SharedClient:AttemptTimeout"] = "00:00:01",
                ["HttpResilience:Clients:SharedClient:RetryCount"] = "1",
                ["HttpResilience:Clients:SharedClient:RetryDelay"] = "00:00:00.001",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerFailureRatio"] = "1",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerMinimumThroughput"] = "100",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerSamplingDuration"] = "00:00:30",
                ["HttpResilience:Clients:SharedClient:CircuitBreakerBreakDuration"] = "00:00:05"
            })
            .AddInMemoryCollection(overrides)
            .Build();

        ServiceCollection services = [];
        services.AddLogging(logging =>
        {
            if (loggerProvider is not null)
            {
                logging.AddProvider(loggerProvider);
            }
        });
        services
            .AddHttpClient("SharedClient", client => client.BaseAddress = new Uri("https://shared-client.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience(configuration, "SharedClient");
        services.AddSingleton(_ => new HttpResilienceMetrics(meterName));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void EnableMetric(MeterListener listener, string meterName, string metricName)
    {
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == metricName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
    }

    private static void EnableAllHttpResilienceMetrics(
        MeterListener listener,
        List<ObservedMetric> observed,
        string meterName)
    {
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            observed.Add(new ObservedMetric(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        });

        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            observed.Add(new ObservedMetric(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        });
    }

    private static void AssertObserved(
        IReadOnlyCollection<ObservedMetric> observed,
        string metricName,
        string outcome,
        string? exceptionType = null)
    {
        ObservedMetric? metric = observed.FirstOrDefault(item =>
            item.Name == metricName &&
            string.Equals(item.Tags["outcome"] as string, outcome, StringComparison.Ordinal));

        Assert.NotNull(metric);

        if (metric.Measurement is long longValue)
        {
            Assert.Equal(1L, longValue);
        }

        AssertTags(metric.Tags, "SharedClient", "SharedClient", metric.Tags["operation"] as string ?? "unknown", outcome, exceptionType);
    }

    private static void AssertTags(
        IReadOnlyDictionary<string, object?> tags,
        string client,
        string dependency,
        string operation,
        string outcome,
        string? exceptionType)
    {
        Assert.Equal(client, tags["client"]);
        Assert.Equal(dependency, tags["dependency"]);
        Assert.Equal(operation, tags["operation"]);
        Assert.Equal(outcome, tags["outcome"]);

        if (exceptionType is null)
        {
            Assert.False(tags.ContainsKey("exception_type"));
        }
        else
        {
            Assert.Equal(exceptionType, tags["exception_type"]);
        }

        Assert.Empty(ProhibitedTags.Intersect(tags.Keys));
    }

    private sealed record ObservedMetric(string Name, object Measurement, IReadOnlyDictionary<string, object?> Tags);

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        private readonly object _gate = new();

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return [.. _messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
            => new InMemoryLogger(_messages, _gate);

        public void Dispose()
        {
        }

        private sealed class InMemoryLogger(List<string> messages, object gate) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (gate)
                {
                    messages.Add(formatter(state, exception));
                }
            }
        }
    }
}
