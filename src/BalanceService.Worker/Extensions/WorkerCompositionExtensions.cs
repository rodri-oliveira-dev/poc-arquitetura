using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Messaging.Kafka.Processors;
using BalanceService.Worker.Observability;

namespace BalanceService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddBalanceWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddWorkerObservability(configuration);
        services.AddApplication();
        services
            .AddBalanceInfrastructureCommon()
            .AddBalancePersistence(configuration)
            .AddBalanceRepositories()
            .AddBalanceKafkaConsumer(configuration, environment)
            .AddBalanceLedgerEventsWorker(configuration);

        return services;
    }

    public static IServiceCollection AddBalanceKafkaConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
            return services;

        services.AddSingleton<KafkaMessagingMetrics>();

        services.AddOptions<KafkaConsumerOptions>()
            .Bind(configuration.GetSection(KafkaConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka GroupId nao configurado.")
            .Validate(o => o.Topics is not null && o.Topics.Count > 0, "Kafka Topics nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "Kafka DeadLetterTopic nao configurado.")
            .Validate(o => o.InvalidMessageRetryDelay > TimeSpan.Zero, "Kafka InvalidMessageRetryDelay deve ser maior que zero.")
            .Validate(o => o.ConsumeErrorRetryDelay > TimeSpan.Zero, "Kafka ConsumeErrorRetryDelay deve ser maior que zero.")
            .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "Kafka ProcessingErrorRetryDelay deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<IKafkaDeadLetterProducer, KafkaDeadLetterProducer>();
        services.AddSingleton<LedgerEntryCreatedMessageProcessor>();

        return services;
    }

    public static IServiceCollection AddBalanceLedgerEventsWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
            return services;

        services.AddHostedService<LedgerEventsConsumer>();

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
