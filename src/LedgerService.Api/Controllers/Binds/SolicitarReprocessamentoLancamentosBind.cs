using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Contracts;
using LedgerService.Api.Mappers;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Controllers.Binds;

public static class SolicitarReprocessamentoLancamentosBind
{
    public static SolicitarReprocessamentoLancamentosCommand Bind(
        HttpContext httpContext,
        string idempotencyKey,
        string? correlationId,
        SolicitarReprocessamentoLancamentosRequest request,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ValidateTransportHeaders(idempotencyKey);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return SolicitarReprocessamentoLancamentosMapper.ToCommand(
            request,
            idempotencyKey,
            resolvedCorrelationId,
            authorizedMerchantIds);
    }

    private static void ValidateTransportHeaders(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    nameof(SolicitarReprocessamentoLancamentosCommand.IdempotencyKey),
                    "Idempotency-Key is required.")
            });
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    nameof(SolicitarReprocessamentoLancamentosCommand.IdempotencyKey),
                    "Idempotency-Key must be a valid UUID.")
            });
        }
    }
}
