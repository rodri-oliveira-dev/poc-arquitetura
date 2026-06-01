using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Events;

namespace LedgerService.Application.Lancamentos.Services;

public static class LedgerEntryCreatedEventFactory
{
    public static LedgerEntryCreatedV1 Create(LancamentoDto response, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new LedgerEntryCreatedV1(
            response.Id,
            response.Type,
            response.Amount,
            response.CreatedAt,
            response.MerchantId,
            response.OccurredAt,
            response.Description,
            correlationId,
            response.ExternalReference);
    }
}
