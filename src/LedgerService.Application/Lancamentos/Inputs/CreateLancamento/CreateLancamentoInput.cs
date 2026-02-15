namespace LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

public sealed record CreateLancamentoInput(
    string MerchantId,
    string Type,
    string Amount,
    string? Description,
    string? ExternalReference,
    string IdempotencyKey,
    string CorrelationId);