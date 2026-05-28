using LedgerService.Api.Extensions;
using LedgerService.Worker.Estornos;
using LedgerService.Worker.Outbox;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Kafka.Processors;
using LedgerService.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Worker.Tests.Composition;

public sealed class ProcessCompositionPolicyTests
{
    [Fact]
    public void LedgerServiceApi_should_not_host_worker_services()
    {
        var services = new ServiceCollection();

        services.AddLedgerApiComposition(CreateConfiguration(), CreateEnvironment());

        services.NotContainHostedService<OutboxPublisherService>();
        services.NotContainHostedService<EstornoLancamentoProcessorService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_host_expected_services_when_flags_are_enabled()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(), CreateEnvironment());

        services.ContainHostedService<OutboxPublisherService>();
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

        services.NotContainHostedService<OutboxPublisherService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
        services.ContainHostedService<EstornoLancamentoProcessorService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_reject_unsupported_messaging_provider()
    {
        var services = new ServiceCollection();

        var act = () => services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub"
        }), CreateEnvironment());

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("Unsupported messaging provider 'PubSub'.", ex.Message);
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

        services.ContainHostedService<OutboxPublisherService>();
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
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Outbox Publisher BatchSize*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_estorno_processor_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Estornos:Processor:PollingIntervalSeconds"] = "0"
        });

        var act = () => provider.GetRequiredService<IOptions<EstornoProcessingOptions>>().Value;
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Estornos Processor PollingIntervalSeconds*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_reprocessamento_consumer_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Reprocessamentos:Consumer:Topic"] = ""
        });

        var act = () => provider.GetRequiredService<IOptions<ReprocessamentoLancamentosConsumerOptions>>().Value;
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Reprocessamentos Consumer Topic*").Replace("\\*", ".*") + "$", ex.Message);
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
