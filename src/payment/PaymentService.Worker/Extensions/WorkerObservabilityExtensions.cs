using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentService.Worker.HostedServices;
using PaymentService.Worker.Observability;

namespace PaymentService.Worker.Extensions;

public static class WorkerObservabilityExtensions
{
    public static IServiceCollection AddPaymentWorkerObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ServiceName), "Observability OpenTelemetry ServiceName nao configurado.")
            .ValidateOnStart();

        services.AddSingleton<PaymentInboxWorkerMetrics>();
        services.AddSingleton<PaymentLedgerWorkerMetrics>();

        var otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (otelOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
                .WithTracing(tracing =>
                {
                    tracing.AddSource(PaymentInboxWorkerService.ActivitySourceName);
                    tracing.AddSource(PaymentLedgerWorkerService.ActivitySourceName);

                    if (otelOptions.UseConsoleExporter)
                        tracing.AddConsoleExporter();

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddMeter(PaymentInboxWorkerMetrics.MeterName)
                        .AddMeter(PaymentLedgerWorkerMetrics.MeterName);

                    if (otelOptions.UseConsoleExporter)
                        metrics.AddConsoleExporter();

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                });
        }

        return services;
    }
}
