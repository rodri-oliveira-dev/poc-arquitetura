using ApiDefaults.Middlewares;

using FluentValidation;
using FluentValidation.Results;

using PaymentService.Api.Contracts.Requests;
using PaymentService.Api.Mappers;
using PaymentService.Application.Payments.Commands;

namespace PaymentService.Api.Controllers.Binds;

public static class CreatePaymentBind
{
    public static CreatePaymentCommand Bind(
        HttpContext httpContext,
        string idempotencyKey,
        string? correlationId,
        CreatePaymentRequest? request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ValidateTransportHeaders(idempotencyKey);
        var validRequest = ValidateRequestBody(request);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        return PaymentMapper.ToCommand(validRequest, idempotencyKey, resolvedCorrelationId);
    }

    private static void ValidateTransportHeaders(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreatePaymentCommand.IdempotencyKey), "Idempotency-Key is required.")
            ]);
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreatePaymentCommand.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            ]);
        }
    }

    private static CreatePaymentRequest ValidateRequestBody(CreatePaymentRequest? request)
    {
        return request switch
        {
            null => throw new ValidationException(
            [
                new ValidationFailure("$", "Request body is required.")
            ]),
            { Amount: null } => throw new ValidationException(
                [
                    new ValidationFailure(nameof(CreatePaymentRequest.Amount), "Amount is required.")
                ]),
            _ => request
        };
    }
}
