using BalanceService.Api.Extensions;
using BalanceService.Infrastructure.Messaging.Kafka;
using BalanceService.Worker.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BalanceService.UnitTests.Tests;

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

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
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
            })
            .Build();
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
        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(THostedService));
    }

    public static void NotContainHostedService<THostedService>(this IServiceCollection services)
        where THostedService : IHostedService
    {
        services.Should().NotContain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(THostedService));
    }
}
