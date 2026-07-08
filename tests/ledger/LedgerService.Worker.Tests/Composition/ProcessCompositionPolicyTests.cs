using LedgerService.Api.Extensions;
using LedgerService.Worker.Estornos;
using LedgerService.Worker.Extensions;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Kafka.Producers;
using LedgerService.Worker.Messaging.PubSub.Configuration;
using LedgerService.Worker.Messaging.PubSub.Producers;
using LedgerService.Worker.Outbox;

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
    public void LedgerServiceWorker_should_use_kafka_when_provider_is_explicitly_kafka()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "Kafka"
        }), CreateEnvironment());

        services.ContainSingleton<IOutboxMessagePublisher, KafkaOutboxMessagePublisher>();
        services.NotContainSingleton<IOutboxMessagePublisher, PubSubOutboxMessagePublisher>();
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
            ["Messaging:Provider"] = "Kafka",
            ["Kafka:Enabled"] = "false"
        }), CreateEnvironment());

        services.NotContainHostedService<OutboxPublisherService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
        services.ContainHostedService<EstornoLancamentoProcessorService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_use_pubsub_legacy_when_provider_is_explicitly_pubsub()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Enabled"] = "true",
            ["PubSub:Producer:ProjectId"] = "poc-project",
            ["PubSub:Producer:DefaultTopicId"] = "ledger-events"
        }), CreateEnvironment());

        services.ContainSingleton<IOutboxMessagePublisher, PubSubOutboxMessagePublisher>();
        services.NotContainSingleton<IOutboxMessagePublisher, KafkaOutboxMessagePublisher>();
        services.ContainHostedService<OutboxPublisherService>();
        services.NotContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_use_kafka_when_provider_is_not_configured()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(), CreateEnvironment());

        services.ContainSingleton<IOutboxMessagePublisher, KafkaOutboxMessagePublisher>();
        services.NotContainSingleton<IOutboxMessagePublisher, PubSubOutboxMessagePublisher>();
        services.ContainHostedService<OutboxPublisherService>();
        services.ContainHostedService<ReprocessamentoLancamentosConsumerService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_not_host_pubsub_outbox_worker_when_pubsub_is_disabled()
    {
        var services = new ServiceCollection();

        services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Enabled"] = "false"
        }), CreateEnvironment());

        services.NotContainHostedService<OutboxPublisherService>();
        services.ContainHostedService<EstornoLancamentoProcessorService>();
    }

    [Fact]
    public void LedgerServiceWorker_should_reject_unknown_messaging_provider()
    {
        var services = new ServiceCollection();

        IServiceCollection act()
        {
            return services.AddLedgerWorkerComposition(CreateConfiguration(new Dictionary<string, string?>
            {
                ["Messaging:Provider"] = "RabbitMq"
            }), CreateEnvironment());
        }

        var ex = Assert.Throws<InvalidOperationException>((Func<IServiceCollection>)act);
        Assert.Equal("Unsupported messaging provider 'RabbitMq'.", ex.Message);
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

        OutboxPublisherOptions act()
        {
            return provider.GetRequiredService<IOptions<OutboxPublisherOptions>>().Value;
        }

        var ex = Assert.Throws<OptionsValidationException>((Func<OutboxPublisherOptions>)act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Outbox Publisher BatchSize*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_pubsub_producer_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "PubSub",
            ["PubSub:Producer:ProjectId"] = "",
            ["PubSub:Producer:DefaultTopicId"] = "ledger-events"
        });

        PubSubProducerOptions act()
        {
            return provider.GetRequiredService<IOptions<PubSubProducerOptions>>().Value;
        }

        var ex = Assert.Throws<OptionsValidationException>((Func<PubSubProducerOptions>)act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*PubSub ProjectId*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_estorno_processor_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Estornos:Processor:PollingIntervalSeconds"] = "0"
        });

        EstornoProcessingOptions act()
        {
            return provider.GetRequiredService<IOptions<EstornoProcessingOptions>>().Value;
        }

        var ex = Assert.Throws<OptionsValidationException>((Func<EstornoProcessingOptions>)act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Estornos Processor PollingIntervalSeconds*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void LedgerServiceWorker_should_validate_reprocessamento_consumer_options()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Messaging:Provider"] = "Kafka",
            ["Reprocessamentos:Consumer:Topic"] = ""
        });

        ReprocessamentoLancamentosConsumerOptions act()
        {
            return provider.GetRequiredService<IOptions<ReprocessamentoLancamentosConsumerOptions>>().Value;
        }

        var ex = Assert.Throws<OptionsValidationException>((Func<ReprocessamentoLancamentosConsumerOptions>)act);
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
            ["Jwt:Issuer"] = "https://issuer.example",
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
            {
                values[item.Key] = item.Value;
            }
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

static file class HostedServiceAssertions
{
    public static void ContainSingleton<TService, TImplementation>(this IServiceCollection services)
    {
        Assert.Contains(services, d =>
            d.ServiceType == typeof(TService) &&
            d.ImplementationType == typeof(TImplementation) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    public static void NotContainSingleton<TService, TImplementation>(this IServiceCollection services)
    {
        Assert.DoesNotContain(services, d =>
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
