using Confluent.Kafka;

namespace PocArquitetura.KafkaWorkerDefaults;

public static class KafkaOffsetResetParser
{
    public static AutoOffsetReset Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }
}
