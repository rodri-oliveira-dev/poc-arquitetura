using BalanceService.Worker.HostedServices;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BalanceService.Worker.Extensions;

public static class WorkerObservabilityExtensions
{
    public static IServiceCollection AddWorkerObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ServiceName), "Observability OpenTelemetry ServiceName nao configurado.")
            .ValidateOnStart();

        var otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        services.AddHostedService(sp =>
            new WorkerLifecycleLogService(
                otelOptions.ServiceName,
                sp.GetRequiredService<ILogger<WorkerLifecycleLogService>>()));

        if (otelOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
                .WithTracing(tracing =>
                {
                    tracing.AddSource(LedgerEntryCreatedMessageProcessor.ActivitySourceName);

                    if (otelOptions.UseConsoleExporter)
                        tracing.AddConsoleExporter();

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddMeter(KafkaMessagingMetrics.MeterName);

                    if (otelOptions.UseConsoleExporter)
                        metrics.AddConsoleExporter();

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                });
        }

        return services;
    }
}
