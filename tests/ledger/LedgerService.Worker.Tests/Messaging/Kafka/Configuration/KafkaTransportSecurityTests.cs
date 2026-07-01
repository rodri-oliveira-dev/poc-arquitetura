using LedgerService.Worker.Extensions;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Processors;

using Confluent.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Moq;

namespace LedgerService.Worker.Tests.Messaging.Kafka.Configuration;

public sealed class KafkaTransportSecurityTests
{
    [Fact]
    public void AddLedgerKafkaProducer_should_allow_plaintext_kafka_in_development()
    {
        using var provider = CreateProvider(Environments.Development, "Plaintext");

        var options = provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

        Assert.Equal("Plaintext", options.SecurityProtocol);
    }

    [Fact]
    public void AddLedgerKafkaProducer_should_reject_plaintext_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "Plaintext");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value);
    }

    [Fact]
    public void AddLedgerKafkaProducer_should_allow_ssl_kafka_in_production()
    {
        using var provider = CreateProvider(Environments.Production, "SSL");

        var options = provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

        Assert.Equal("SSL", options.SecurityProtocol);
    }

    [Fact]
    public void ApplySecurity_should_configure_producer_security_fields()
    {
        var config = new ProducerConfig();
        var options = new KafkaProducerOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "Scram-Sha-256",
            SaslUsername = "user",
            SaslPassword = "secret",
            SslCaLocation = "/certs/ca.pem"
        };

        config.ApplySecurity(options);

        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha256, config.SaslMechanism);
        Assert.Equal("user", config.SaslUsername);
        Assert.Equal("secret", config.SaslPassword);
        Assert.Equal("/certs/ca.pem", config.SslCaLocation);
        Assert.False(KafkaClientConfigExtensions.IsPlaintext(options));
    }

    [Fact]
    public void ApplySecurity_should_configure_reprocessamento_consumer_security_fields()
    {
        var config = new ConsumerConfig();
        var options = new ReprocessamentoLancamentosConsumerOptions
        {
            SecurityProtocol = "SASL_PLAINTEXT",
            SaslMechanism = "OAuthBearer",
            SaslUsername = "user",
            SaslPassword = "secret",
            SslCaLocation = "/certs/ca.pem"
        };

        config.ApplySecurity(options);

        Assert.Equal(SecurityProtocol.SaslPlaintext, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.OAuthBearer, config.SaslMechanism);
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
        var config = new ProducerConfig();
        var options = new KafkaProducerOptions { SecurityProtocol = securityProtocol };

        Assert.Throws<InvalidOperationException>(() => config.ApplySecurity(options));
    }

    [Fact]
    public void ApplySecurity_should_reject_invalid_sasl_mechanism()
    {
        var config = new ProducerConfig();
        var options = new KafkaProducerOptions
        {
            SecurityProtocol = "SSL",
            SaslMechanism = "invalid"
        };

        Assert.Throws<InvalidOperationException>(() => config.ApplySecurity(options));
    }

    private static ServiceProvider CreateProvider(string environmentName, string securityProtocol)
    {
        var services = new ServiceCollection();
        services.AddLedgerKafkaProducer(CreateConfiguration(securityProtocol), CreateEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(string securityProtocol)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=ignore;Username=ignore;Password=ignore",
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
