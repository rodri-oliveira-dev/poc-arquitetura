using TransferService.Application.Transferencias.Events;

namespace TransferService.Infrastructure.Messaging.Kafka;

public sealed class TransferenciaKafkaTopicOptions
{
    public const string SectionName = "TransferService:Worker:Topics";

    public string Solicitada { get; set; } = "transfer.transferencia.solicitada";
    public string DebitoCriado { get; set; } = "transfer.transferencia.debito-criado";
    public string CreditoCriado { get; set; } = "transfer.transferencia.credito-criado";
    public string Concluida { get; set; } = "transfer.transferencia.concluida";
    public string CompensacaoSolicitada { get; set; } = "transfer.transferencia.compensacao-solicitada";
    public string Compensada { get; set; } = "transfer.transferencia.compensada";
    public string Falhou { get; set; } = "transfer.transferencia.falhou";

    public string ResolveTopic(string eventType)
        => eventType switch
        {
            TransferenciaSolicitadaV1.Type => Solicitada,
            TransferenciaDebitoCriadoV1.Type => DebitoCriado,
            TransferenciaCreditoCriadoV1.Type => CreditoCriado,
            TransferenciaConcluidaV1.Type => Concluida,
            TransferenciaCompensacaoSolicitadaV1.Type => CompensacaoSolicitada,
            TransferenciaCompensadaV1.Type => Compensada,
            TransferenciaFalhouV1.Type => Falhou,
            _ => throw new InvalidOperationException($"Event type '{eventType}' nao possui topico Kafka configurado.")
        };
}
