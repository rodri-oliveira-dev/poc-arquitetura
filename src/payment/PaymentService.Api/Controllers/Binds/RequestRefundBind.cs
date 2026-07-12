using FluentValidation;
using FluentValidation.Results;

using PaymentService.Api.Contracts.Requests;
using PaymentService.Application.Payments.Commands;

namespace PaymentService.Api.Controllers.Binds;

public static class RequestRefundBind
{
    public static RequestRefundCommand Bind(
        HttpContext httpContext,
        Guid paymentId,
        string idempotencyKey,
        string? correlationId,
        RequestRefundRequest? request,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (request is null)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(RequestRefundRequest), "Request body is required.")]);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(RequestRefundCommand.IdempotencyKey), "Idempotency-Key is required.")]);
        }

        return new RequestRefundCommand(
            paymentId,
            idempotencyKey,
            request.Amount,
            request.Reason,
            request.ExternalReference,
            correlationId,
            authorizedMerchantIds);
    }
}
