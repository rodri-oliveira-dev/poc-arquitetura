namespace TransferService.Worker.Ledger;

public interface ILedgerServiceClient
{
    Task<LedgerLancamentoResult> CreateLancamentoAsync(
        CreateLedgerLancamentoRequest request,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<LedgerEstornoResult> SolicitarEstornoAsync(
        Guid lancamentoId,
        SolicitarLedgerEstornoRequest request,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken);
}

public sealed record CreateLedgerLancamentoRequest(
    string MerchantId,
    string Type,
    decimal Amount,
    string? Description,
    string? ExternalReference);

public sealed record LedgerLancamentoResult(Guid LancamentoId);

public sealed record SolicitarLedgerEstornoRequest(string Motivo);

public sealed record LedgerEstornoResult(Guid EstornoId);
