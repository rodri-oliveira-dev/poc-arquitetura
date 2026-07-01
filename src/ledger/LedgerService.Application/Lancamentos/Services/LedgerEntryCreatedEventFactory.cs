using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Events;

namespace LedgerService.Application.Lancamentos.Services;

public static class LedgerEntryCreatedEventFactory
{
    public const string SupportedCurrency = "BRL";

    public static LedgerEntryCreatedV2 Create(LancamentoDto response, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new LedgerEntryCreatedV2(
            response.Id,
            response.Type,
            response.Amount,
            SupportedCurrency,
            response.CreatedAt,
            response.MerchantId,
            response.OccurredAt,
            response.Description,
            correlationId,
            response.ExternalReference);
    }
}
