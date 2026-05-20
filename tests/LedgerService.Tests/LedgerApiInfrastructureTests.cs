using LedgerService.Infrastructure;
using LedgerService.Infrastructure.Estornos;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Outbox;
using LedgerService.Infrastructure.Reprocessamentos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace LedgerService.Tests;

public sealed class LedgerApiInfrastructureTests
{
    [Fact]
    public void AddLedgerApiInfrastructure_should_not_register_kafka_producer()
    {
        var services = new ServiceCollection();

        services.AddLedgerApiInfrastructure(CreateConfiguration(), CreateEnvironment());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IOutboxEventProducer));

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.GenericTypeArguments.Contains(typeof(KafkaProducerOptions)));
    }

    [Fact]
    public void AddInfrastructure_should_not_register_worker_hosted_services()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CreateConfiguration(), CreateEnvironment());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(OutboxKafkaPublisherService));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(EstornoLancamentoProcessorService));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(ReprocessamentoLancamentosConsumerService));
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=ignore;Username=ignore;Password=ignore",
                ["Kafka:Enabled"] = "true",
                ["Kafka:Producer:BootstrapServers"] = "localhost:9092"
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        return environment.Object;
    }
}
