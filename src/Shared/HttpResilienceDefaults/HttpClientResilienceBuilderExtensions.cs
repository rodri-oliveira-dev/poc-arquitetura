using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace HttpResilienceDefaults;

public static class HttpClientResilienceBuilderExtensions
{
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

        builder
            .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.TotalRequestTimeout.Timeout = options.TotalTimeout;
                resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
                resilience.Retry.MaxRetryAttempts = options.RetryCount;
                resilience.Retry.Delay = options.RetryDelay;

                if (!options.RetryUnsafeHttpMethods)
                {
                    resilience.Retry.DisableForUnsafeHttpMethods();
                }

                resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
                resilience.CircuitBreaker.MinimumThroughput = options.CircuitBreakerMinimumThroughput;
                resilience.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
                resilience.CircuitBreaker.BreakDuration = options.CircuitBreakerBreakDuration;
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
