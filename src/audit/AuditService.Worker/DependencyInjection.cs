using AuditService.Application;
using AuditService.Infrastructure;
using AuditService.Worker.HostedServices;
using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Messaging.Kafka.DeadLetter;
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
        services.AddAuditKafkaConsumer(configuration, environment);
        services.AddAuditWorkerObservability(configuration);

        var workerOptions = configuration.GetSection(AuditWorkerOptions.SectionName).Get<AuditWorkerOptions>()
            ?? new AuditWorkerOptions();
        var consumerOptions = configuration.GetSection(AuditRecordRequestedConsumerOptions.SectionName).Get<AuditRecordRequestedConsumerOptions>()
            ?? new AuditRecordRequestedConsumerOptions();

        if (workerOptions.Enabled && consumerOptions.Enabled)
        {
            services.AddHostedService<AuditRecordRequestedConsumerService>();
        }
        else
        {
            services.AddHostedService<AuditWorkerPlaceholderService>();
        }

        return services;
    }

    private static void AddAuditWorkerOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuditWorkerOptions>()
            .Bind(configuration.GetSection(AuditWorkerOptions.SectionName))
            .Validate(o => o.IdleDelay > TimeSpan.Zero, "AuditService Worker IdleDelay deve ser maior que zero.")
            .ValidateOnStart();
    }

    private static void AddAuditKafkaConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<AuditRecordRequestedConsumerOptions>()
            .Bind(configuration.GetSection(AuditRecordRequestedConsumerOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.BootstrapServers), "AuditRecordRequested Consumer BootstrapServers nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.GroupId), "AuditRecordRequested Consumer GroupId nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Topic), "AuditRecordRequested Consumer Topic nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "AuditRecordRequested Consumer DeadLetterTopic nao configurado.")
            .Validate(o => !o.Enabled || o.DeadLetterMessageTimeoutMs > 0, "AuditRecordRequested Consumer DeadLetterMessageTimeoutMs deve ser maior que zero.")
            .Validate(o => !o.Enabled || o.MaxProcessingAttempts > 0, "AuditRecordRequested Consumer MaxProcessingAttempts deve ser maior que zero.")
            .Validate(o => !o.Enabled || o.ProcessingRetryDelay > TimeSpan.Zero, "AuditRecordRequested Consumer ProcessingRetryDelay deve ser maior que zero.")
            .Validate(o => !o.Enabled || o.ConsumeErrorRetryDelay > TimeSpan.Zero, "AuditRecordRequested Consumer ConsumeErrorRetryDelay deve ser maior que zero.")
            .Validate(o => !o.Enabled || o.ProcessingErrorRetryDelay > TimeSpan.Zero, "AuditRecordRequested Consumer ProcessingErrorRetryDelay deve ser maior que zero.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .ValidateOnStart();

        services.AddSingleton<AuditWorkerMetrics>();
        services.AddSingleton<AuditRecordRequestedValidator>();
        services.AddSingleton<IAuditKafkaDeadLetterProducerFactory, ConfluentAuditKafkaDeadLetterProducerFactory>();
        services.AddSingleton<IAuditRecordDeadLetterPublisher, KafkaAuditRecordDeadLetterPublisher>();
        services.AddScoped<IAuditRecordRequestedProcessor, AuditRecordRequestedProcessor>();
        services.AddSingleton<IAuditKafkaConsumerFactory, ConfluentAuditKafkaConsumerFactory>();
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || string.Equals(environment.EnvironmentName, "Local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase);

    private static void AddAuditWorkerObservability(
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
    }
}
