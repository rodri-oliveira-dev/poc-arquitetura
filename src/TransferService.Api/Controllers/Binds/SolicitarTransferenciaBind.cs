using ApiDefaults.Middlewares;

using FluentValidation;
using FluentValidation.Results;

using TransferService.Api.Contracts.Requests;
using TransferService.Api.Mappers;
using TransferService.Application.Transferencias.Commands;

namespace TransferService.Api.Controllers.Binds;

public static class SolicitarTransferenciaBind
{
    public static SolicitarTransferenciaCommand Bind(
        HttpContext httpContext,
        string idempotencyKey,
        string? correlationId,
        SolicitarTransferenciaRequest? request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ValidateTransportHeaders(idempotencyKey);
        var validRequest = ValidateRequestBody(request);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return TransferenciaMapper.ToCommand(validRequest, idempotencyKey, resolvedCorrelationId);
    }

    private static void ValidateTransportHeaders(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(SolicitarTransferenciaCommand.IdempotencyKey), "Idempotency-Key is required.")
            ]);
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(SolicitarTransferenciaCommand.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            ]);
        }
    }

    private static SolicitarTransferenciaRequest ValidateRequestBody(SolicitarTransferenciaRequest? request)
    {
        return request switch
        {
            null => throw new ValidationException(
            [
                new ValidationFailure("$", "Request body is required.")
            ]),
            { Amount: null } => throw new ValidationException(
                [
                    new ValidationFailure(nameof(SolicitarTransferenciaRequest.Amount), "Amount is required.")
                ]),
            _ => request
        };
    }
}
