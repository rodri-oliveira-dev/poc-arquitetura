using FluentAssertions;
using LedgerService.Api.Extensions;
using LedgerService.Infrastructure.Estornos;
using LedgerService.Infrastructure.Outbox;
using LedgerService.Infrastructure.Reprocessamentos;
using LedgerService.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Worker.Tests.Tests;

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

    [Fact]
    public void LedgerServiceWorker_should_not_host_kafka_workers_when_kafka_is_disabled()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kafka:Enabled"] = "false"
        }), CreateEnvironment());

        services.NotContainHostedService<OutboxKafkaPublisherService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
        services.ContainHostedService<EstornoLancamentoProcessorService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_not_host_optional_processors_when_their_flags_are_disabled()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Estornos:Processor:Enabled"] = "false",
            ["Reprocessamentos:Consumer:Enabled"] = "false"
        }), CreateEnvironment());

        services.ContainHostedService<OutboxKafkaPublisherService>();
        services.NotContainHostedService<EstornoLancamentoProcessorService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_outbox_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Outbox:Publisher:BatchSize"] = "0"
        });

        var act = () => provider.GetRequiredService<IOptions<OutboxPublisherOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Outbox Publisher BatchSize*");
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_estorno_processor_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Estornos:Processor:PollingIntervalSeconds"] = "0"
        });

        var act = () => provider.GetRequiredService<IOptions<EstornoProcessingOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Estornos Processor PollingIntervalSeconds*");
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_reprocessamento_consumer_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Reprocessamentos:Consumer:Topic"] = ""
        });

        var act = () => provider.GetRequiredService<IOptions<ReprocessamentoLancamentosConsumerOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Reprocessamentos Consumer Topic*");
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> overrides)
    {
        var services = new ServiceCollection();
        services.AddLedgerWorkerComposition(CreateConfiguration(overrides), CreateEnvironment());
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
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
