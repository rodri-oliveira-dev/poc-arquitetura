using TransferService.Application.Transferencias.Events;

namespace TransferService.Infrastructure.Messaging.Kafka;

public sealed class TransferenciaKafkaTopicOptions
{
    public const string SectionName = "TransferService:KafkaTopics";

    public string Transferencias { get; set; } = "transferencias.saga.events.v1";
    public string TransferenciasFalhas { get; set; } = "transferencias.saga.failures.v1";

    public string ResolveTopic(string eventType)
        => eventType == TransferenciaFalhouV1.Type ? TransferenciasFalhas : Transferencias;
}
