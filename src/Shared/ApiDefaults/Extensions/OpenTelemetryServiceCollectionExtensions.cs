using ApiDefaults.RateLimiting;

using HttpResilienceDefaults;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiDefaults.Extensions;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredApiOpenTelemetryDefaults<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, bool> isEnabled,
        Func<TOptions, string> getServiceName,
        Func<TOptions, bool> useConsoleExporter,
        Func<TOptions, string?> getOtlpEndpoint,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentNullException.ThrowIfNull(getServiceName);
        ArgumentNullException.ThrowIfNull(useConsoleExporter);
        ArgumentNullException.ThrowIfNull(getOtlpEndpoint);

        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName));

        TOptions otelOptions = configuration.GetSection(sectionName).Get<TOptions>()
            ?? new TOptions();

        return !isEnabled(otelOptions)
            ? services
            : services.AddApiOpenTelemetryDefaults(
                getServiceName(otelOptions),
                useConsoleExporter(otelOptions),
                getOtlpEndpoint(otelOptions),
                configureTracing,
                configureMetrics);
    }

    public static IServiceCollection AddApiOpenTelemetryDefaults(
        this IServiceCollection services,
        string serviceName,
        bool useConsoleExporter,
        string? otlpEndpoint,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                configureTracing?.Invoke(tracing);
                AddTraceExporters(tracing, useConsoleExporter, otlpEndpoint);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(ApiRateLimitMetrics.MeterName)
                    .AddMeter(HttpResilienceMetrics.MeterName);

                configureMetrics?.Invoke(metrics);
                AddMetricExporters(metrics, useConsoleExporter, otlpEndpoint);
            });

        return services;
    }

    private static void AddTraceExporters(
        TracerProviderBuilder tracing,
        bool useConsoleExporter,
        string? otlpEndpoint)
    {
        if (useConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    }

    private static void AddMetricExporters(
        MeterProviderBuilder metrics,
        bool useConsoleExporter,
        string? otlpEndpoint)
    {
        if (useConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    }
}
