using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Contracts;
using LedgerService.Api.Mappers;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Controllers.Binds;

public static class SolicitarEstornoLancamentoBind
{
    public static SolicitarEstornoLancamentoCommand Bind(
        HttpContext httpContext,
        Guid lancamentoId,
        string idempotencyKey,
        string? correlationId,
        SolicitarEstornoLancamentoRequest request,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ValidateTransportHeaders(idempotencyKey);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return SolicitarEstornoLancamentoMapper.ToCommand(
            lancamentoId,
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
                new ValidationFailure(nameof(SolicitarEstornoLancamentoCommand.IdempotencyKey), "Idempotency-Key is required.")
            });
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(SolicitarEstornoLancamentoCommand.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            });
        }
    }
}
