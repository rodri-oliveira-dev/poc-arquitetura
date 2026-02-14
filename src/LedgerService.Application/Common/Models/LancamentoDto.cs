namespace LedgerService.Application.Common.Models;

public sealed record LancamentoDto(
    string Id,
    string MerchantId,
    string Type,
    string Amount,
    string Currency,
    string OccurredAt,
    string? Description,
    string? ExternalReference,
    string CreatedAt);