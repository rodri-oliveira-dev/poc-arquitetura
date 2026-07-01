namespace TransferService.Application.Transferencias.Events;

public sealed record TransferenciaDebitoCriadoV1(
    Guid TransferenciaId,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? CorrelationId)
    : TransferenciaSagaEvent(
        Type,
        TransferenciaId,
        SourceMerchantId,
        DestinationMerchantId,
        Amount,
        OccurredAt,
        CorrelationId)
{
    public const string Type = "TransferenciaDebitoCriado.v1";
}
