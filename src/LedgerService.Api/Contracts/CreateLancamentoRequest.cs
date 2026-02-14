namespace LedgerService.Api.Contracts;

public sealed record CreateLancamentoRequest(
    string MerchantId,
    string Type,
    string Amount,
    string Currency,
    string OccurredAt,
    string? Description,
    string? ExternalReference);