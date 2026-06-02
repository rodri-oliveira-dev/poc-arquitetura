using BalanceService.Api.Extensions;
using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.PubSub.Configuration;
using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Messaging.PubSub.DeadLetter;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Observability;
using BalanceService.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace BalanceService.Worker.Tests.Composition;

public sealed class ProcessCompositionPolicyTests
{
    [Fact]
    public void BalanceServiceApi_should_not_host_ledger_events_consumer()
    {
        var services = new ServiceCollection();

        services.AddBalanceApiComposition(CreateConfiguration(), CreateEnvironment());

        services.NotContainHostedService<LedgerEventsConsumer>();
    }

    [Fact]
    public void BalanceServiceWorker_should_host_kafka_ledger_events_consumer_when_kafka_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "Kafka"
        }), CreateEnvironment());

        services.ContainHostedService<LedgerEventsConsumer>();
        services.NotContainHostedService<LedgerEventsPubSubConsumer>();
    }

    [Fact]
    public void BalanceServiceWorker_should_not_host_ledger_events_consumer_when_kafka_is_disabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Enabled"] = "false"
        }), CreateEnvironment());

        services.NotContainHostedService<LedgerEventsConsumer>();
    }

    [Fact]
    public void BalanceServiceWorker_should_host_pubsub_ledger_events_consumer_when_pubsub_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Enabled"] = "true",
            ["PubSub:Consumer:ProjectId"] = "poc-project",
            ["PubSub:Consumer:SubscriptionId"] = "ledger-events-balance",
            ["PubSub:Consumer:DeadLetterTopicId"] = "ledger-events-dlq"
        }), CreateEnvironment());

        services.ContainSingleton<IDeadLetterPublisher, PubSubDeadLetterPublisher>();
        services.ContainHostedService<LedgerEventsPubSubConsumer>();
        services.NotContainHostedService<LedgerEventsConsumer>();
    }

    [Fact]
    public void BalanceServiceWorker_should_not_host_pubsub_ledger_events_consumer_when_pubsub_is_disabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Enabled"] = "false"
        }), CreateEnvironment());

        services.NotContainHostedService<LedgerEventsPubSubConsumer>();
    }

    [Fact]
    public void BalanceServiceWorker_should_reject_unsupported_messaging_provider()
    {
        var services = new ServiceCollection();

        var act = () => services.AddBalanceWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "RabbitMq"
        }), CreateEnvironment());

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("Unsupported messaging provider 'RabbitMq'.", ex.Message);
    }

    [Fact]
    public void BalanceServiceWorker_should_register_consumer_dependencies_when_kafka_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(), CreateEnvironment());
        Assert.Contains(services, d => d.ServiceType == typeof(LedgerEntryCreatedMessageProcessor));
        Assert.Contains(services, d => d.ServiceType == typeof(IDeadLetterPublisher));
        Assert.Contains(services, d => d.ServiceType == typeof(MessagingMetrics));
        Assert.Contains(services, d => d.ServiceType == typeof(IDailyBalanceRepository));
        Assert.Contains(services, d => d.ServiceType == typeof(IProcessedEventRepository));
    }

    [Fact]
    public void BalanceServiceWorker_should_validate_pubsub_consumer_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Consumer:ProjectId"] = "",
            ["PubSub:Consumer:SubscriptionId"] = "ledger-events-balance",
            ["PubSub:Consumer:DeadLetterTopicId"] = "ledger-events-dlq"
        });

        var act = () => provider.GetRequiredService<IOptions<PubSubConsumerOptions>>().Value;
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*PubSub ProjectId*").Replace("\\*", ".*") + "$", ex.Message);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> overrides)
    {
        var services = new ServiceCollection();
        services.AddBalanceWorkerComposition(CreateConfiguration(overrides), CreateEnvironment());
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=ignore;Username=ignore;Password=ignore",
            ["Jwt:Issuer"] = "https://auth-api",
            ["Jwt:Audience"] = "balance-api",
            ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
            ["Kafka:Enabled"] = "true",
            ["Kafka:Consumer:BootstrapServers"] = "localhost:9092",
            ["Kafka:Consumer:SecurityProtocol"] = "Plaintext",
            ["Kafka:Consumer:GroupId"] = "balance-service",
            ["Kafka:Consumer:Topics:0"] = "ledger.ledgerentry.created",
            ["Kafka:Consumer:DeadLetterTopic"] = "ledger.ledgerentry.created.dlq"
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
                values[item.Key] = item.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Test");
        return environment.Object;
    }
}

file static class HostedServiceAssertions
{
    public static void ContainSingleton<TService, TImplementation>(this IServiceCollection services)
    {
        Assert.Contains(services, d =>
            d.ServiceType == typeof(TService) &&
            d.ImplementationType == typeof(TImplementation) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    public static void ContainHostedService<THostedService>(this IServiceCollection services)
        where THostedService : IHostedService
    {
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(THostedService));
    }

    public static void NotContainHostedService<THostedService>(this IServiceCollection services)
        where THostedService : IHostedService
    {
        Assert.DoesNotContain(services, d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(THostedService));
    }
}
