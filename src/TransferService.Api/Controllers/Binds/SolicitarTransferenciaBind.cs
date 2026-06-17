using FluentValidation;
using FluentValidation.Results;
using TransferService.Api.Contracts.Requests;
using TransferService.Api.Mappers;
using ApiDefaults.Middlewares;
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
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(SolicitarTransferenciaCommand.IdempotencyKey), "Idempotency-Key is required.")
            });
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(SolicitarTransferenciaCommand.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            });
        }
    }

    private static SolicitarTransferenciaRequest ValidateRequestBody(SolicitarTransferenciaRequest? request)
    {
        if (request is not null)
            return request;

        throw new ValidationException(new[]
        {
            new ValidationFailure("$", "Request body is required.")
        });
    }
}
