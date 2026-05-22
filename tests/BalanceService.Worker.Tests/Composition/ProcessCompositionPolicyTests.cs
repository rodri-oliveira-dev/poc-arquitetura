using BalanceService.Api.Extensions;
using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Kafka.DeadLetter;
using BalanceService.Worker.Messaging.Kafka.Processors;
using BalanceService.Worker.Observability;
using BalanceService.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public void BalanceServiceWorker_should_host_ledger_events_consumer_when_kafka_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(), CreateEnvironment());

        services.ContainHostedService<LedgerEventsConsumer>();
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
    public void BalanceServiceWorker_should_register_consumer_dependencies_when_kafka_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddBalanceWorkerComposition(CreateConfiguration(), CreateEnvironment());
        Assert.Contains(services, d => d.ServiceType == typeof(LedgerKafkaMessageProcessor));
        Assert.Contains(services, d => d.ServiceType == typeof(IKafkaDeadLetterProducer));
        Assert.Contains(services, d => d.ServiceType == typeof(KafkaMessagingMetrics));
        Assert.Contains(services, d => d.ServiceType == typeof(IDailyBalanceRepository));
        Assert.Contains(services, d => d.ServiceType == typeof(IProcessedEventRepository));
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
