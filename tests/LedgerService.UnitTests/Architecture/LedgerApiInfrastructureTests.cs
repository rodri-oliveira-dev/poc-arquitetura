using LedgerService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.UnitTests.Architecture;

public sealed class LedgerApiInfrastructureTests
{
    [Fact]
    public void AddLedgerApiInfrastructure_should_not_register_kafka_producer()
    {
        var services = new ServiceCollection();

        services.AddLedgerApiInfrastructure(CreateConfiguration(), CreateEnvironment());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.Name == "IOutboxEventProducer");

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
            descriptor.ServiceType.GenericTypeArguments[0].Name == "KafkaProducerOptions");
    }

    [Fact]
    public void AddInfrastructure_should_not_register_worker_hosted_services()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CreateConfiguration(), CreateEnvironment());

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
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
