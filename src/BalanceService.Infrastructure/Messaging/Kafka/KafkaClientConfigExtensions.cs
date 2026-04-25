using Confluent.Kafka;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public static class KafkaClientConfigExtensions
{
    public static void ApplySecurity(this ClientConfig config, KafkaConsumerOptions options)
    {
        config.SecurityProtocol = ParseSecurityProtocol(options.SecurityProtocol);

        if (!string.IsNullOrWhiteSpace(options.SaslMechanism))
            config.SaslMechanism = ParseSaslMechanism(options.SaslMechanism);

        if (!string.IsNullOrWhiteSpace(options.SaslUsername))
            config.SaslUsername = options.SaslUsername;

        if (!string.IsNullOrWhiteSpace(options.SaslPassword))
            config.SaslPassword = options.SaslPassword;

        if (!string.IsNullOrWhiteSpace(options.SslCaLocation))
            config.SslCaLocation = options.SslCaLocation;
    }

    public static bool IsPlaintext(KafkaConsumerOptions options)
        => ParseSecurityProtocol(options.SecurityProtocol) is SecurityProtocol.Plaintext;

    private static SecurityProtocol ParseSecurityProtocol(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal) switch
        {
            "plaintext" => SecurityProtocol.Plaintext,
            "ssl" => SecurityProtocol.Ssl,
            "saslplaintext" => SecurityProtocol.SaslPlaintext,
            "saslssl" => SecurityProtocol.SaslSsl,
            _ => throw new InvalidOperationException("Kafka SecurityProtocol deve ser Plaintext, SSL, SASL_PLAINTEXT ou SASL_SSL.")
        };
    }

    private static SaslMechanism ParseSaslMechanism(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal) switch
        {
            "plain" => SaslMechanism.Plain,
            "scramsha256" => SaslMechanism.ScramSha256,
            "scramsha512" => SaslMechanism.ScramSha512,
            "oauthbearer" => SaslMechanism.OAuthBearer,
            "gssapi" => SaslMechanism.Gssapi,
            _ => throw new InvalidOperationException("Kafka SaslMechanism deve ser Plain, ScramSha256, ScramSha512, OAuthBearer ou Gssapi.")
        };
    }
}
