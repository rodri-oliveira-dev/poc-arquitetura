using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Messaging.PubSub.Configuration;
using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Messaging.PubSub.DeadLetter;
using BalanceService.Worker.Observability;

namespace BalanceService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    private const string MessagingProviderConfigurationKey = "Messaging:Provider";
    private const string KafkaProvider = "Kafka";
    private const string PubSubProvider = "PubSub";

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
            .AddBalanceMessaging(configuration, environment);

        return services;
    }

    public static IServiceCollection AddBalanceMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration.GetValue<string>(MessagingProviderConfigurationKey) ?? KafkaProvider;

        return provider.Trim().ToUpperInvariant() switch
        {
            "KAFKA" => services.AddBalanceKafkaMessaging(configuration, environment),
            "PUBSUB" => services.AddBalancePubSubMessaging(configuration, environment),
            _ => throw new InvalidOperationException($"Unsupported messaging provider '{provider}'.")
        };
    }

    public static IServiceCollection AddBalanceKafkaMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services
            .AddBalanceKafkaConsumer(configuration, environment)
            .AddBalanceLedgerEventsWorker(configuration);
    }

    public static IServiceCollection AddBalancePubSubMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services
            .AddBalancePubSubConsumer(configuration, environment)
            .AddBalancePubSubLedgerEventsWorker(configuration);
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

        services.AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();
        services.AddSingleton<LedgerEntryCreatedMessageProcessor>();

        return services;
    }

    public static IServiceCollection AddBalancePubSubConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment _)
    {
        var pubSubEnabled = configuration.GetValue<bool>($"{PubSubProvider}:Enabled", defaultValue: true);
        if (!pubSubEnabled)
            return services;

        services.AddSingleton<KafkaMessagingMetrics>();

        services.AddOptions<PubSubConsumerOptions>()
            .Bind(configuration.GetSection(PubSubConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ProjectId), "PubSub ProjectId nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SubscriptionId), "PubSub SubscriptionId nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopicId), "PubSub DeadLetterTopicId nao configurado.")
            .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "PubSub Consumer ProcessingErrorRetryDelay deve ser maior que zero.")
            .Validate(o => o.SubscriberClientCount > 0, "PubSub Consumer SubscriberClientCount deve ser maior que zero.")
            .Validate(o => o.AckDeadlineSeconds is >= 1 and <= 600, "PubSub Consumer AckDeadlineSeconds deve estar entre 1 e 600.")
            .ValidateOnStart();

        services.AddSingleton<IDeadLetterPublisher, PubSubDeadLetterPublisher>();
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

    public static IServiceCollection AddBalancePubSubLedgerEventsWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var pubSubEnabled = configuration.GetValue<bool>($"{PubSubProvider}:Enabled", defaultValue: true);
        if (!pubSubEnabled)
            return services;

        services.AddHostedService<LedgerEventsPubSubConsumer>();

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
