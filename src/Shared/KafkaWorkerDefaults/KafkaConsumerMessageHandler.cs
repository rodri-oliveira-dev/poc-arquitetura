using Confluent.Kafka;

namespace PocArquitetura.KafkaWorkerDefaults;

public static class KafkaConsumerMessageHandler
{
    public static async Task ProcessAsync<TMessage>(
        ConsumeResult<string, string>? result,
        Func<ConsumeResult<string, string>, TMessage> map,
        Func<TMessage, CancellationToken, Task<bool>> process,
        Action<ConsumeResult<string, string>> commit,
        Action? afterCommit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(commit);

        if (result?.Message?.Value is null)
        {
            return;
        }

        var message = map(result);
        if (await process(message, cancellationToken))
        {
            commit(result);
            afterCommit?.Invoke();
        }
    }
}
