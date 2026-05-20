using FluentAssertions;
using LedgerService.Api.Extensions;
using LedgerService.Infrastructure.Estornos;
using LedgerService.Infrastructure.Outbox;
using LedgerService.Infrastructure.Reprocessamentos;
using LedgerService.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace LedgerService.UnitTests.Tests;

public sealed class ProcessCompositionPolicyTests
{
    [Fact]
    public void LedgerServiceApi_should_not_host_worker_services()
    {
        var services = new ServiceCollection();

        services.AddLedgerApiComposition(CreateConfiguration(), CreateEnvironment());

        services.NotContainHostedService<OutboxKafkaPublisherService>();
        services.NotContainHostedService<EstornoLancamentoProcessorService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_host_expected_services_when_flags_are_enabled()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(), CreateEnvironment());

        services.ContainHostedService<OutboxKafkaPublisherService>();
        services.ContainHostedService<EstornoLancamentoProcessorService>();
        services.ContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=ignore;Username=ignore;Password=ignore",
                ["Jwt:Issuer"] = "https://auth-api",
                ["Jwt:Audience"] = "ledger-api",
                ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                ["Kafka:Enabled"] = "true",
                ["Kafka:Producer:BootstrapServers"] = "localhost:9092",
                ["Kafka:Producer:SecurityProtocol"] = "Plaintext",
                ["Outbox:Publisher:PollingIntervalSeconds"] = "1",
                ["Outbox:Publisher:BatchSize"] = "10",
                ["Outbox:Publisher:MaxParallelism"] = "1",
                ["Outbox:Publisher:MaxAttempts"] = "3",
                ["Outbox:Publisher:BaseBackoffSeconds"] = "1",
                ["Outbox:Publisher:LockDurationSeconds"] = "30",
                ["Estornos:Processor:Enabled"] = "true",
                ["Estornos:Processor:PollingIntervalSeconds"] = "1",
                ["Estornos:Processor:BatchSize"] = "10",
                ["Reprocessamentos:Consumer:Enabled"] = "true",
                ["Reprocessamentos:Consumer:BootstrapServers"] = "localhost:9092",
                ["Reprocessamentos:Consumer:SecurityProtocol"] = "Plaintext",
                ["Reprocessamentos:Consumer:GroupId"] = "ledger-service",
                ["Reprocessamentos:Consumer:Topic"] = "ledger.reprocessamentos"
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
