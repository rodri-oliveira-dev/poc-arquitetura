namespace TransferService.Infrastructure.Messaging.Kafka;

public sealed record TransferenciaKafkaPublicationMetadata(
    string Topic,
    string MessageKey,
    IReadOnlyDictionary<string, string> Headers);
