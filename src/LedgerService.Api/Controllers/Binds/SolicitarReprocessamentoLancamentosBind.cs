using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Api.Mappers;
using ApiDefaults.Middlewares;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Controllers.Binds;

public static class SolicitarReprocessamentoLancamentosBind
{
    public static SolicitarReprocessamentoLancamentosCommand Bind(
        HttpContext httpContext,
        string idempotencyKey,
        string? correlationId,
        SolicitarReprocessamentoLancamentosRequest? request,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ValidateTransportHeaders(idempotencyKey);
        var validRequest = ValidateRequestBody(request);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return SolicitarReprocessamentoLancamentosMapper.ToCommand(
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

    private static SolicitarReprocessamentoLancamentosRequest ValidateRequestBody(SolicitarReprocessamentoLancamentosRequest? request)
    {
        if (request is not null)
            return request;

        throw new ValidationException(new[]
        {
            new ValidationFailure("$", "Request body is required.")
        });
    }
}
