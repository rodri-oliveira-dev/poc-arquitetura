using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Api.Mappers;
using ApiDefaults.Middlewares;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Controllers.Binds;

public static class SolicitarEstornoLancamentoBind
{
    public static SolicitarEstornoLancamentoCommand Bind(
        HttpContext httpContext,
        Guid lancamentoId,
        string idempotencyKey,
        string? correlationId,
        SolicitarEstornoLancamentoRequest? request,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ValidateTransportHeaders(idempotencyKey);
        var validRequest = ValidateRequestBody(request);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return SolicitarEstornoLancamentoMapper.ToCommand(
            lancamentoId,
            validRequest,
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

    private static SolicitarEstornoLancamentoRequest ValidateRequestBody(SolicitarEstornoLancamentoRequest? request)
    {
        if (request is not null)
            return request;

        throw new ValidationException(new[]
        {
            new ValidationFailure("$", "Request body is required.")
        });
    }
}
