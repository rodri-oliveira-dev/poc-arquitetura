using LedgerService.Application;
using LedgerService.Infrastructure;
using LedgerService.Worker.Estornos;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Producers;
using LedgerService.Worker.Outbox;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Kafka.Processors;

namespace LedgerService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddLedgerWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddWorkerObservability(configuration);
        services.AddApplication();
        services
            .AddLedgerInfrastructureCommon()
            .AddLedgerPersistence(configuration)
            .AddLedgerRepositories()
            .AddLedgerKafkaProducer(configuration, environment)
            .AddLedgerOutboxWorker(configuration)
            .AddLedgerEstornoWorker(configuration)
            .AddLedgerReprocessamentoWorker(configuration, environment);

        return services;
    }

    public static IServiceCollection AddLedgerKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
            return services;

        services.AddOptions<KafkaProducerOptions>()
            .Bind(configuration.GetSection(KafkaProducerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .ValidateOnStart();

        services.AddSingleton<IOutboxMessagePublisher, KafkaOutboxMessagePublisher>();

        return services;
    }

    public static IServiceCollection AddLedgerOutboxWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
            return services;

        services.AddOptions<OutboxPublisherOptions>()
            .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
            .Validate(o => o.PollingIntervalSeconds > 0, "Outbox Publisher PollingIntervalSeconds deve ser maior que zero.")
            .Validate(o => o.BatchSize > 0, "Outbox Publisher BatchSize deve ser maior que zero.")
            .Validate(o => o.MaxParallelism > 0, "Outbox Publisher MaxParallelism deve ser maior que zero.")
            .Validate(o => o.MaxAttempts > 0, "Outbox Publisher MaxAttempts deve ser maior que zero.")
            .Validate(o => o.BaseBackoffSeconds > 0, "Outbox Publisher BaseBackoffSeconds deve ser maior que zero.")
            .Validate(o => o.LockDurationSeconds > 0, "Outbox Publisher LockDurationSeconds deve ser maior que zero.")
            .ValidateOnStart();

        services.AddHostedService<OutboxPublisherService>();

        return services;
    }

    public static IServiceCollection AddLedgerEstornoWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var estornoProcessorEnabled = configuration.GetValue<bool>("Estornos:Processor:Enabled", defaultValue: true);
        if (!estornoProcessorEnabled)
            return services;

        services.AddOptions<EstornoProcessingOptions>()
            .Bind(configuration.GetSection(EstornoProcessingOptions.SectionName))
            .Validate(o => o.PollingIntervalSeconds > 0, "Estornos Processor PollingIntervalSeconds deve ser maior que zero.")
            .Validate(o => o.BatchSize > 0, "Estornos Processor BatchSize deve ser maior que zero.")
            .ValidateOnStart();

        services.AddHostedService<EstornoLancamentoProcessorService>();

        return services;
    }

    public static IServiceCollection AddLedgerReprocessamentoWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
            return services;

        var reprocessamentoConsumerEnabled = configuration.GetValue<bool>(
            $"{ReprocessamentoLancamentosConsumerOptions.SectionName}:Enabled",
            defaultValue: true);
        if (!reprocessamentoConsumerEnabled)
            return services;

        services.AddOptions<ReprocessamentoLancamentosConsumerOptions>()
            .Bind(configuration.GetSection(ReprocessamentoLancamentosConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Reprocessamentos Consumer BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Reprocessamentos Consumer GroupId nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Reprocessamentos Consumer Topic nao configurado.")
            .Validate(o => o.ConsumeErrorRetryDelay > TimeSpan.Zero, "Reprocessamentos Consumer ConsumeErrorRetryDelay deve ser maior que zero.")
            .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "Reprocessamentos Consumer ProcessingErrorRetryDelay deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<ReprocessamentoLancamentosMessageProcessor>();
        services.AddHostedService<ReprocessamentoLancamentosConsumerService>();

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
