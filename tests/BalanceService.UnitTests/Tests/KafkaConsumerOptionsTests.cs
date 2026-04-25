using BalanceService.Infrastructure;
using BalanceService.Infrastructure.Messaging.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class KafkaConsumerOptionsTests
{
    [Fact]
    public void KafkaConsumerOptions_should_preserve_current_retry_delay_defaults()
    {
        var options = new KafkaConsumerOptions();

        options.SecurityProtocol.Should().Be("Plaintext");
        options.InvalidMessageRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.ConsumeErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.ProcessingErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(5));
        options.DeadLetterMessageTimeoutMs.Should().Be(30000);
    }

    [Fact]
    public void KafkaConsumerOptions_should_bind_retry_delays_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:Consumer:InvalidMessageRetryDelay"] = "00:00:03",
                ["Kafka:Consumer:ConsumeErrorRetryDelay"] = "00:00:04",
                ["Kafka:Consumer:ProcessingErrorRetryDelay"] = "00:00:06",
                ["Kafka:Consumer:DeadLetterTopic"] = "ledger.ledgerentry.created.dlq"
            })
            .Build();

        var options = configuration
            .GetSection(KafkaConsumerOptions.SectionName)
            .Get<KafkaConsumerOptions>();

        options.Should().NotBeNull();
        options!.InvalidMessageRetryDelay.Should().Be(TimeSpan.FromSeconds(3));
        options.ConsumeErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(4));
        options.ProcessingErrorRetryDelay.Should().Be(TimeSpan.FromSeconds(6));
        options.DeadLetterTopic.Should().Be("ledger.ledgerentry.created.dlq");
    }

    [Fact]
    public void AddInfrastructure_should_allow_plaintext_kafka_in_development()
    {
        using var provider = CreateProvider(Environments.Development, "Plaintext");

        var options = provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;

        options.SecurityProtocol.Should().Be("Plaintext");
    }

    [Fact]
    public void AddInfrastructure_should_reject_plaintext_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "Plaintext");

        var act = () => provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*PLAINTEXT*Development/Local*");
    }

    [Fact]
    public void AddInfrastructure_should_allow_ssl_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "SSL");

        var options = provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;

        options.SecurityProtocol.Should().Be("SSL");
    }

    private static ServiceProvider CreateProvider(string environmentName, string securityProtocol)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateInfrastructureConfiguration(securityProtocol), CreateEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateInfrastructureConfiguration(string securityProtocol)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignore;Username=ignore;Password=ignore",
                ["Kafka:Enabled"] = "true",
                ["Kafka:Consumer:BootstrapServers"] = "localhost:9092",
                ["Kafka:Consumer:SecurityProtocol"] = securityProtocol,
                ["Kafka:Consumer:GroupId"] = "balance-service",
                ["Kafka:Consumer:Topics:0"] = "ledger.ledgerentry.created",
                ["Kafka:Consumer:DeadLetterTopic"] = "ledger.ledgerentry.created.dlq"
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
