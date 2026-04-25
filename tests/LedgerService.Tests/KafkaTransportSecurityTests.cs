using LedgerService.Infrastructure;
using LedgerService.Infrastructure.Messaging.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Tests;

public sealed class KafkaTransportSecurityTests
{
    [Fact]
    public void AddInfrastructure_should_allow_plaintext_kafka_in_development()
    {
        using var provider = CreateProvider(Environments.Development, "Plaintext");

        var options = provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

        Assert.Equal("Plaintext", options.SecurityProtocol);
    }

    [Fact]
    public void AddInfrastructure_should_reject_plaintext_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "Plaintext");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value);
    }

    [Fact]
    public void AddInfrastructure_should_allow_ssl_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "SSL");

        var options = provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

        Assert.Equal("SSL", options.SecurityProtocol);
    }

    private static ServiceProvider CreateProvider(string environmentName, string securityProtocol)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(securityProtocol), CreateEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(string securityProtocol)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignore;Username=ignore;Password=ignore",
                ["Kafka:Enabled"] = "true",
                ["Kafka:Producer:BootstrapServers"] = "localhost:9092",
                ["Kafka:Producer:SecurityProtocol"] = securityProtocol
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
