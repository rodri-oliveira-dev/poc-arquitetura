using Microsoft.Extensions.Logging;

namespace BalanceService.Infrastructure.Observability;

internal static partial class LedgerEventsConsumerLogMessages
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "Kafka consumer error: {Reason} (IsFatal={IsFatal})")]
    internal static partial void KafkaConsumerError(this ILogger logger, string reason, bool isFatal);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "LedgerEventsConsumer started (groupId={GroupId}, clientId={ClientId}, topics={Topics})")]
    internal static partial void ConsumerStarted(this ILogger logger, string groupId, string clientId, string topics);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "Mensagem processada/DLQ confirmada e offset commitado")]
    internal static partial void KafkaMessageCommitted(this ILogger logger);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Erro ao consumir do Kafka. Vai retentar.")]
    internal static partial void KafkaConsumeError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Error, Message = "Erro ao processar mensagem do Kafka. Offset nao sera commitado (retry). topic={Topic} partition={Partition} offset={Offset}")]
    internal static partial void KafkaProcessingError(this ILogger logger, Exception exception, string? topic, int? partition, long? offset);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Error, Message = "Erro ao processar mensagem do Kafka. Offset não será commitado (retry). topic={Topic} partition={Partition} offset={Offset}")]
    internal static partial void KafkaProcessingErrorWithKafkaException(this ILogger logger, Exception exception, string? topic, int? partition, long? offset);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "LedgerEventsConsumer stopped")]
    internal static partial void ConsumerStopped(this ILogger logger);
}
