using System.Globalization;

using LedgerService.Api.Contracts;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

namespace LedgerService.Api.Mappers;

public static class CreateLancamentoInputMapper
{
    public static CreateLancamentoInput ToInput(
        CreateLancamentoRequest request,
        string idempotencyKey,
        string correlationId)
        => new(
            request.MerchantId,
            (request.Type ?? string.Empty).Trim().ToUpperInvariant(),
            request.Amount.ToString(CultureInfo.InvariantCulture),
            request.Description,
            request.ExternalReference,
            idempotencyKey,
            correlationId);
}
