using TransferService.Domain.Sagas;

namespace TransferService.Application.Transferencias.Events;

public static class TransferenciaSagaEventFactory
{
    public static TransferenciaSolicitadaV1 TransferenciaSolicitada(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaDebitoCriadoV1 TransferenciaDebitoCriado(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaCreditoCriadoV1 TransferenciaCreditoCriado(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaConcluidaV1 TransferenciaConcluida(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaCompensacaoSolicitadaV1 TransferenciaCompensacaoSolicitada(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaCompensadaV1 TransferenciaCompensada(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    public static TransferenciaFalhouV1 TransferenciaFalhou(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var data = CreateData(saga, correlationId, occurredAt);
        return new(data.TransferenciaId, data.SourceMerchantId, data.DestinationMerchantId, data.Amount, data.OccurredAt, data.CorrelationId);
    }

    private static TransferenciaSagaEventData CreateData(
        TransferenciaSaga saga,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(saga);

        return new(
            saga.Id,
            saga.SourceMerchantId.Value,
            saga.DestinationMerchantId.Value,
            saga.Amount.Value,
            occurredAt,
            NormalizeCorrelationId(correlationId));
    }

    private static string? NormalizeCorrelationId(string? correlationId)
        => string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();

    private sealed record TransferenciaSagaEventData(
        Guid TransferenciaId,
        string SourceMerchantId,
        string DestinationMerchantId,
        decimal Amount,
        DateTimeOffset OccurredAt,
        string? CorrelationId);
}
