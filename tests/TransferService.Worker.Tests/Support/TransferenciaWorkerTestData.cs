using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;
using TransferService.Infrastructure.Persistence.Outbox;

namespace TransferService.Worker.Tests.Support;

internal static class TransferenciaWorkerTestData
{
    public static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    public static TransferenciaSaga CreateSaga(
        string sourceMerchantId = "merchant-source",
        string destinationMerchantId = "merchant-destination",
        decimal amount = 100m,
        string? correlationId = "correlation-1")
    {
        var saga = new TransferenciaSaga(
            new MerchantId(sourceMerchantId),
            new MerchantId(destinationMerchantId),
            new TransferAmount(amount),
            Now);

        saga.RegisterRequestMetadata("idem-1", "hash-1", correlationId, Now);
        return saga;
    }

    public static TransferenciaOutboxMessage CreateOutboxMessage(
        Guid? aggregateId = null,
        string eventType = TransferenciaCompensadaV1.Type,
        string payload = /*lang=json,strict*/ "{\"transferenciaId\":\"transferencia-1\"}",
        string topic = "transfer.transferencia.compensada",
        string? correlationId = "correlation-1")
    {
        var id = aggregateId ?? Guid.NewGuid();
        return new TransferenciaOutboxMessage(
            "TransferenciaSaga",
            id,
            eventType,
            payload,
            topic,
            id.ToString(),
            correlationId,
            Now,
            Now);
    }
}
