using LedgerService.Infrastructure;
using LedgerService.Infrastructure.Messaging.Kafka;
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
