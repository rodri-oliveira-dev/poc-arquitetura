using Confluent.Kafka;

namespace PocArquitetura.KafkaWorkerDefaults;

public static class KafkaConsumerLifecycle
{
    public static void Close(Action close)
    {
        ArgumentNullException.ThrowIfNull(close);

        try
        {
            close();
        }
        catch (KafkaException)
        {
            // Shutdown must not fail the hosted service stop path.
        }
    }
}
