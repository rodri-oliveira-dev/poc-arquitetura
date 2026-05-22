using BalanceService.Api.Extensions;
using BalanceService.Worker.Extensions;
using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Consumers;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Configuration;

public sealed class KafkaConsumerOptionsTests
{
    [Fact]
    public void KafkaConsumerOptions_should_preserve_current_retry_delay_defaults()
    {
        var options = new KafkaConsumerOptions();
        Assert.Equal("Plaintext", options.SecurityProtocol);
        Assert.Equal(TimeSpan.FromSeconds(2), options.InvalidMessageRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(2), options.ConsumeErrorRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ProcessingErrorRetryDelay);
        Assert.Equal(30000, options.DeadLetterMessageTimeoutMs);
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
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromSeconds(3), options!.InvalidMessageRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(4), options.ConsumeErrorRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(6), options.ProcessingErrorRetryDelay);
        Assert.Equal("ledger.ledgerentry.created.dlq", options.DeadLetterTopic);
    }

    [Fact]
    public void AddBalanceKafkaConsumer_should_allow_plaintext_kafka_in_development()
    {
        using var provider = CreateProvider(Environments.Development, "Plaintext");

        var options = provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;
        Assert.Equal("Plaintext", options.SecurityProtocol);
    }

    [Fact]
    public void AddBalanceKafkaConsumer_should_reject_plaintext_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "Plaintext");

        var act = () => provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;

        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^.*PLAINTEXT.*Development/Local.*$", ex.Message);
    }

    [Fact]
    public void AddBalanceKafkaConsumer_should_allow_ssl_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "SSL");

        var options = provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;
        Assert.Equal("SSL", options.SecurityProtocol);
    }

    [Fact]
    public void ApplySecurity_should_configure_consumer_security_fields()
    {
        var config = new ConsumerConfig();
        var options = new KafkaConsumerOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "Scram-Sha-512",
            SaslUsername = "user",
            SaslPassword = "secret",
            SslCaLocation = "/certs/ca.pem"
        };

        config.ApplySecurity(options);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha512, config.SaslMechanism);
        Assert.Equal("user", config.SaslUsername);
        Assert.Equal("secret", config.SaslPassword);
        Assert.Equal("/certs/ca.pem", config.SslCaLocation);
        Assert.False(KafkaClientConfigExtensions.IsPlaintext(options));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void ApplySecurity_should_reject_invalid_security_protocol(string securityProtocol)
    {
        var config = new ConsumerConfig();
        var options = new KafkaConsumerOptions { SecurityProtocol = securityProtocol };

        var act = () => config.ApplySecurity(options);

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("SecurityProtocol", ex.Message);
    }

    [Fact]
    public void ApplySecurity_should_reject_invalid_sasl_mechanism()
    {
        var config = new ConsumerConfig();
        var options = new KafkaConsumerOptions
        {
            SecurityProtocol = "SSL",
            SaslMechanism = "invalid"
        };

        var act = () => config.ApplySecurity(options);

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("SaslMechanism", ex.Message);
    }

    [Fact]
    public void BalanceServiceApiComposition_should_not_host_ledger_events_consumer()
    {
        var services = new ServiceCollection();
        services.AddBalanceApiComposition(CreateInfrastructureConfiguration("SSL"), CreateEnvironment(Environments.Production));

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(LedgerEventsConsumer));
    }

    private static ServiceProvider CreateProvider(string environmentName, string securityProtocol)
    {
        var services = new ServiceCollection();
        services.AddBalanceKafkaConsumer(CreateInfrastructureConfiguration(securityProtocol), CreateEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateInfrastructureConfiguration(string securityProtocol)
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
