namespace TransferService.Application.Transferencias.Events;

public abstract record TransferenciaSagaEvent(
    string EventType,
    Guid TransferenciaId,
    string SourceMerchantId,
    string DestinationMerchantId,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? CorrelationId);
