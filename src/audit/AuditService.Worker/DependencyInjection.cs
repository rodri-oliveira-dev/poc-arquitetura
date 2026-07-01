using AuditService.Application;
using AuditService.Infrastructure;
using AuditService.Worker.HostedServices;
using AuditService.Worker.Observability;
using AuditService.Worker.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AuditService.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddAuditApplication();
        services.AddAuditInfrastructure(configuration, environment);
        services.AddAuditWorkerOptions(configuration);
        services.AddAuditWorkerObservability(configuration);
        services.AddHostedService<AuditWorkerPlaceholderService>();

        return services;
    }

    private static IServiceCollection AddAuditWorkerOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuditWorkerOptions>()
            .Bind(configuration.GetSection(AuditWorkerOptions.SectionName))
            .Validate(o => o.IdleDelay > TimeSpan.Zero, "AuditService Worker IdleDelay deve ser maior que zero.")
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddAuditWorkerObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ServiceName), "Observability OpenTelemetry ServiceName nao configurado.")
            .ValidateOnStart();

        var otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (otelOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
                .WithTracing(tracing =>
                {
                    if (otelOptions.UseConsoleExporter)
                    {
                        tracing.AddConsoleExporter();
                    }

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                    {
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddMeter("AuditService.Worker");

                    if (otelOptions.UseConsoleExporter)
                    {
                        metrics.AddConsoleExporter();
                    }

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                    {
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                    }
                });
        }

        return services;
    }
}
