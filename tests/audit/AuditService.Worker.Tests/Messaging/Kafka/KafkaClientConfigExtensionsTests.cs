using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Messaging.Kafka.DeadLetter;

using Confluent.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Worker.Tests.Messaging.Kafka;

public sealed class KafkaClientConfigExtensionsTests
{
    [Fact]
    public void CreateConfig_should_map_minimal_consumer_options()
    {
        var options = new AuditRecordRequestedConsumerOptions
        {
            BootstrapServers = "localhost:9092",
            GroupId = "audit-group",
            ClientId = "audit-client",
            AutoOffsetReset = "Latest",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false,
            SecurityProtocol = "Plaintext"
        };

        ConsumerConfig config = ConfluentAuditKafkaConsumerFactory.CreateConfig(options);

        Assert.Equal("localhost:9092", config.BootstrapServers);
        Assert.Equal("audit-group", config.GroupId);
        Assert.Equal("audit-client", config.ClientId);
        Assert.Equal(AutoOffsetReset.Latest, config.AutoOffsetReset);
        Assert.False(config.EnableAutoCommit);
        Assert.False(config.EnableAutoOffsetStore);
        Assert.False(config.AllowAutoCreateTopics);
        Assert.Equal(SecurityProtocol.Plaintext, config.SecurityProtocol);
    }

    [Fact]
    public void ConsumerFactory_Create_should_return_confluent_consumer_wrapper_for_valid_options()
    {
        var factory = new ConfluentAuditKafkaConsumerFactory(
            Microsoft.Extensions.Options.Options.Create(new AuditRecordRequestedConsumerOptions
            {
                BootstrapServers = "localhost:9092",
                GroupId = "audit-group",
                ClientId = "audit-client",
                SecurityProtocol = "Plaintext"
            }),
            NullLogger<ConfluentAuditKafkaConsumerFactory>.Instance);

        using IAuditKafkaConsumer consumer = factory.Create();

        Assert.IsType<ConfluentAuditKafkaConsumer>(consumer);
    }

    [Fact]
    public void CreateConfig_should_map_minimal_dead_letter_producer_options()
    {
        var options = new AuditRecordRequestedConsumerOptions
        {
            BootstrapServers = "localhost:9092",
            ClientId = "audit-client",
            DeadLetterMessageTimeoutMs = 1234,
            SecurityProtocol = "SSL",
            SslCaLocation = "/tmp/ca.pem"
        };

        ProducerConfig config = ConfluentAuditKafkaDeadLetterProducerFactory.CreateConfig(options);

        Assert.Equal("localhost:9092", config.BootstrapServers);
        Assert.Equal("audit-client-dlq", config.ClientId);
        Assert.Equal(Acks.All, config.Acks);
        Assert.True(config.EnableIdempotence);
        Assert.Equal(1234, config.MessageTimeoutMs);
        Assert.Equal(SecurityProtocol.Ssl, config.SecurityProtocol);
        Assert.Equal("/tmp/ca.pem", config.SslCaLocation);
    }

    [Fact]
    public void DeadLetterProducerFactory_Create_should_return_confluent_producer_wrapper_for_valid_options()
    {
        var factory = new ConfluentAuditKafkaDeadLetterProducerFactory(
            Microsoft.Extensions.Options.Options.Create(new AuditRecordRequestedConsumerOptions
            {
                BootstrapServers = "localhost:9092",
                ClientId = "audit-client",
                SecurityProtocol = "Plaintext"
            }),
            NullLogger<ConfluentAuditKafkaDeadLetterProducerFactory>.Instance);

        using IAuditKafkaDeadLetterProducer producer = factory.Create();

        Assert.IsType<ConfluentAuditKafkaDeadLetterProducer>(producer);
    }

    [Fact]
    public void ApplySecurity_should_map_sasl_settings()
    {
        var config = new ClientConfig();
        var options = new AuditRecordRequestedConsumerOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "Scram-Sha-512",
            SaslUsername = "audit-user",
            SaslPassword = "audit-password",
            SslCaLocation = "/tmp/ca.pem"
        };

        config.ApplySecurity(options);

        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha512, config.SaslMechanism);
        Assert.Equal("audit-user", config.SaslUsername);
        Assert.Equal("audit-password", config.SaslPassword);
        Assert.Equal("/tmp/ca.pem", config.SslCaLocation);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void ApplySecurity_should_reject_invalid_security_protocol(string securityProtocol)
    {
        var config = new ClientConfig();
        var options = new AuditRecordRequestedConsumerOptions
        {
            SecurityProtocol = securityProtocol
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => config.ApplySecurity(options));

        Assert.Contains("SecurityProtocol", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySecurity_should_reject_invalid_sasl_mechanism()
    {
        var config = new ClientConfig();
        var options = new AuditRecordRequestedConsumerOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "invalid"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => config.ApplySecurity(options));

        Assert.Contains("SaslMechanism", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Plaintext", true)]
    [InlineData("SSL", false)]
    [InlineData("SASL_PLAINTEXT", false)]
    [InlineData("SASL_SSL", false)]
    public void IsPlaintext_should_identify_plaintext_protocol(string securityProtocol, bool expected)
    {
        var options = new AuditRecordRequestedConsumerOptions
        {
            SecurityProtocol = securityProtocol
        };

        Assert.Equal(expected, KafkaClientConfigExtensions.IsPlaintext(options));
    }

    [Theory]
    [InlineData("Earliest", AutoOffsetReset.Earliest)]
    [InlineData("Latest", AutoOffsetReset.Latest)]
    [InlineData("unexpected", AutoOffsetReset.Earliest)]
    public void ParseAutoOffsetReset_should_fallback_to_earliest(string value, AutoOffsetReset expected)
        => Assert.Equal(expected, ConfluentAuditKafkaConsumerFactory.ParseAutoOffsetReset(value));
}
