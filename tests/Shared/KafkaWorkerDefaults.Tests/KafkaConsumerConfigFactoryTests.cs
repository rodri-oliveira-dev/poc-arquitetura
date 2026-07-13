using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace KafkaWorkerDefaults.Tests;

public sealed class KafkaConsumerConfigFactoryTests
{
    [Fact]
    public void Create_should_map_consumer_and_security_options()
    {
        var options = new TestConsumerConfigOptions
        {
            BootstrapServers = "kafka:9092",
            GroupId = "worker-group",
            ClientId = "worker-client",
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = true,
            AutoOffsetReset = "Latest",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "Scram-Sha-512",
            SaslUsername = "user",
            SaslPassword = "secret",
            SslCaLocation = "/certs/ca.pem"
        };

        ConsumerConfig config = KafkaConsumerConfigFactory.Create(options);

        Assert.Equal("kafka:9092", config.BootstrapServers);
        Assert.Equal("worker-group", config.GroupId);
        Assert.Equal("worker-client", config.ClientId);
        Assert.True(config.EnableAutoCommit);
        Assert.False(config.EnableAutoOffsetStore);
        Assert.True(config.AllowAutoCreateTopics);
        Assert.Equal(AutoOffsetReset.Latest, config.AutoOffsetReset);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.ScramSha512, config.SaslMechanism);
        Assert.Equal("user", config.SaslUsername);
        Assert.Equal("secret", config.SaslPassword);
        Assert.Equal("/certs/ca.pem", config.SslCaLocation);
    }

    [Fact]
    public void Create_should_reject_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => KafkaConsumerConfigFactory.Create(null!));
    }

    [Theory]
    [InlineData("Plaintext", SecurityProtocol.Plaintext)]
    [InlineData("SSL", SecurityProtocol.Ssl)]
    [InlineData("SASL_PLAINTEXT", SecurityProtocol.SaslPlaintext)]
    [InlineData("SASL_SSL", SecurityProtocol.SaslSsl)]
    public void ApplySecurity_should_accept_supported_security_protocols(string value, SecurityProtocol expected)
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions { SecurityProtocol = value };

        KafkaClientSecurity.ApplySecurity(config, options);

        Assert.Equal(expected, config.SecurityProtocol);
    }

    [Fact]
    public void ApplySecurity_should_reject_invalid_security_protocol()
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions { SecurityProtocol = "invalid" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KafkaClientSecurity.ApplySecurity(config, options));

        Assert.Contains("SecurityProtocol", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Plain", SaslMechanism.Plain)]
    [InlineData("ScramSha256", SaslMechanism.ScramSha256)]
    [InlineData("Scram-Sha-512", SaslMechanism.ScramSha512)]
    [InlineData("OAuthBearer", SaslMechanism.OAuthBearer)]
    [InlineData("Gssapi", SaslMechanism.Gssapi)]
    public void ApplySecurity_should_accept_supported_sasl_mechanisms(string value, SaslMechanism expected)
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = value
        };

        KafkaClientSecurity.ApplySecurity(config, options);

        Assert.Equal(expected, config.SaslMechanism);
    }

    [Fact]
    public void ApplySecurity_should_reject_invalid_sasl_mechanism()
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions
        {
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "invalid"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KafkaClientSecurity.ApplySecurity(config, options));

        Assert.Contains("SaslMechanism", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySecurity_should_ignore_empty_optional_sasl_and_ssl_fields()
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions
        {
            SecurityProtocol = "SSL",
            SaslMechanism = "",
            SaslUsername = "",
            SaslPassword = "",
            SslCaLocation = ""
        };

        KafkaClientSecurity.ApplySecurity(config, options);

        Assert.Equal(SecurityProtocol.Ssl, config.SecurityProtocol);
        Assert.Null(config.SaslMechanism);
        Assert.Null(config.SaslUsername);
        Assert.Null(config.SaslPassword);
        Assert.Null(config.SslCaLocation);
    }

    [Fact]
    public void ApplySecurity_should_map_ssl_ca_location_when_present()
    {
        var config = new ClientConfig();
        var options = new TestSecurityOptions
        {
            SecurityProtocol = "SSL",
            SslCaLocation = "/certs/root-ca.pem"
        };

        KafkaClientSecurity.ApplySecurity(config, options);

        Assert.Equal("/certs/root-ca.pem", config.SslCaLocation);
    }

    [Theory]
    [InlineData("Plaintext", true)]
    [InlineData("SSL", false)]
    [InlineData("SASL_PLAINTEXT", false)]
    [InlineData("SASL_SSL", false)]
    public void IsPlaintext_should_identify_plaintext_protocol(string value, bool expected)
    {
        var options = new TestSecurityOptions { SecurityProtocol = value };

        Assert.Equal(expected, KafkaClientSecurity.IsPlaintext(options));
    }

    private sealed class TestConsumerConfigOptions : TestSecurityOptions, IKafkaConsumerConfigOptions
    {
        public string BootstrapServers { get; init; } = string.Empty;
        public string GroupId { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public bool EnableAutoCommit
        {
            get; init;
        }
        public bool EnableAutoOffsetStore
        {
            get; init;
        }
        public bool AllowAutoCreateTopics
        {
            get; init;
        }
        public string AutoOffsetReset { get; init; } = "Earliest";
    }

    private class TestSecurityOptions : IKafkaClientSecurityOptions
    {
        public string SecurityProtocol { get; init; } = "Plaintext";
        public string SaslMechanism { get; init; } = string.Empty;
        public string SaslUsername { get; init; } = string.Empty;
        public string SaslPassword { get; init; } = string.Empty;
        public string SslCaLocation { get; init; } = string.Empty;
    }
}
