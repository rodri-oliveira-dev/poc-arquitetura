using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

namespace LedgerService.UnitTests.Fixtures;

public static class LancamentoFixture
{
    public static CreateLancamentoInput ValidInput(
        string? merchantId = null,
        string? type = null,
        string? amount = null,
        string? idempotencyKey = null,
        string? correlationId = null)
        => new(
            MerchantId: merchantId ?? "m1",
            Type: type ?? "CREDIT",
            Amount: amount ?? "10.00",
            Description: "desc",
            ExternalReference: "ext",
            IdempotencyKey: idempotencyKey ?? Guid.NewGuid().ToString(),
            CorrelationId: correlationId ?? Guid.NewGuid().ToString());
}
