using TransferService.Application.Transferencias.Commands;
using TransferService.Domain.Sagas;

namespace TransferService.UnitTests.Support;

internal static class TransferenciaTestData
{
    public static readonly DateTimeOffset Now = new(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

    public static SolicitarTransferenciaCommand CreateCommand(
        string idempotencyKey = "idem-1",
        string sourceMerchantId = "merchant-source",
        string destinationMerchantId = "merchant-destination",
        decimal amount = 100m,
        string? correlationId = "correlation-1")
        => new(idempotencyKey, sourceMerchantId, destinationMerchantId, amount, correlationId);

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
}
