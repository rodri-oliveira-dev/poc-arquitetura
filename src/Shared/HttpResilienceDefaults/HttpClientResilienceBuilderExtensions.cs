using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace HttpResilienceDefaults;

public static class HttpClientResilienceBuilderExtensions
{
    private static readonly Action<ILogger, string, int, TimeSpan, Exception?> _logHttpRetry =
        LoggerMessage.Define<string, int, TimeSpan>(
            LogLevel.Warning,
            new EventId(1, nameof(_logHttpRetry)),
            "Retry HTTP resiliente agendado. Client={ClientName} Attempt={AttemptNumber} RetryDelay={RetryDelay}");

    private static readonly Action<ILogger, string, TimeSpan, Exception?> _logCircuitOpened =
        LoggerMessage.Define<string, TimeSpan>(
            LogLevel.Warning,
            new EventId(2, nameof(_logCircuitOpened)),
            "Circuit breaker HTTP aberto. Client={ClientName} BreakDuration={BreakDuration}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitHalfOpened =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, nameof(_logCircuitHalfOpened)),
            "Tentativa HTTP em half-open liberada pelo circuit breaker. Client={ClientName}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitClosed =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, nameof(_logCircuitClosed)),
            "Circuit breaker HTTP fechado. Client={ClientName}");

    public static IHttpClientBuilder AddConfiguredHttpResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        string clientName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        HttpClientResilienceOptions options = ReadOptions(configuration, clientName);
        options.Validate(clientName);

        if (!options.Enabled)
        {
            return builder;
        }

        builder.Services.TryAddSingleton<HttpResilienceMetrics>();

        builder
            .AddHttpMessageHandler(serviceProvider =>
            {
                var logger = serviceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("HttpResilienceDefaults.HttpClient");

                return new HttpResilienceMetricsHandler(
                    clientName,
                    serviceProvider.GetRequiredService<HttpResilienceMetrics>(),
                    logger);
            })
            .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var logger = serviceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("HttpResilienceDefaults.HttpClient");
                var metrics = serviceProvider.GetRequiredService<HttpResilienceMetrics>();

                resilience.TotalRequestTimeout.Timeout = options.TotalTimeout;
                resilience.TotalRequestTimeout.OnTimeout = args =>
                {
                    metrics.RecordTimeout(clientName, "unknown");
                    return default;
                };
                resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
                resilience.AttemptTimeout.OnTimeout = args =>
                {
                    metrics.RecordTimeout(clientName, "unknown");
                    return default;
                };
                resilience.Retry.MaxRetryAttempts = options.RetryCount;
                resilience.Retry.Delay = options.RetryDelay;
                resilience.Retry.OnRetry = args =>
                {
                    _logHttpRetry(logger, clientName, args.AttemptNumber + 1, args.RetryDelay, args.Outcome.Exception);
                    metrics.RecordRetry(clientName, "unknown", args.Outcome.Exception);
                    return default;
                };

                if (!options.RetryUnsafeHttpMethods)
                {
                    resilience.Retry.DisableForUnsafeHttpMethods();
                }

                resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
                resilience.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
                resilience.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
                resilience.CircuitBreaker.BreakDuration = options.CircuitBreakerBreakDuration;
                resilience.CircuitBreaker.OnOpened = args =>
                {
                    _logCircuitOpened(logger, clientName, args.BreakDuration, args.Outcome.Exception);
                    metrics.RecordCircuitOpened(clientName, "unknown", args.Outcome.Exception);
                    return default;
                };
                resilience.CircuitBreaker.OnHalfOpened = _ =>
                {
                    _logCircuitHalfOpened(logger, clientName, null);
                    metrics.RecordCircuitHalfOpened(clientName, "unknown");
                    return default;
                };
                resilience.CircuitBreaker.OnClosed = _ =>
                {
                    _logCircuitClosed(logger, clientName, null);
                    metrics.RecordCircuitClosed(clientName, "unknown");
                    return default;
                };
            });

        return builder;
    }

    private static HttpClientResilienceOptions ReadOptions(IConfiguration configuration, string clientName)
    {
        HttpClientResilienceOptions options = new();
        configuration
            .GetSection(HttpClientResilienceOptions.SectionName)
            .GetSection("Clients")
            .GetSection(clientName)
            .Bind(options);

        return options;
    }
}
