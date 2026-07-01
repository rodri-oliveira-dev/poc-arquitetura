using ApiDefaults.Middlewares;

using AuditService.Api.Contracts;
using AuditService.Api.Security;
using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

using FluentValidation;
using FluentValidation.Results;

namespace AuditService.Api.Controllers.Binds;

public static class CreateAuditRecordBind
{
    public static CreateAuditRecordCommand Bind(
        HttpContext httpContext,
        string? idempotencyKey,
        string? correlationId,
        CreateAuditRecordRequest? request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ValidateTransportHeaders(idempotencyKey);
        var validRequest = request ?? throw new ValidationException(
        [
            new ValidationFailure("$", "Request body is required.")
        ]);

        Guid resolvedCorrelationId = ResolveCorrelationId(httpContext, correlationId, validRequest.CorrelationId);

        return new CreateAuditRecordCommand(
            validRequest.OperationId,
            resolvedCorrelationId,
            idempotencyKey!.Trim(),
            validRequest.SourceService,
            validRequest.OperationType,
            validRequest.EntityType,
            validRequest.EntityId,
            validRequest.MerchantId,
            httpContext.User.ResolveActor(validRequest.Actor is null
                ? null
                : new CreateAuditRecordActor(
                    validRequest.Actor.Type,
                    validRequest.Actor.Subject,
                    validRequest.Actor.ClientId)),
            validRequest.Status,
            validRequest.Reason,
            validRequest.Metadata,
            validRequest.OccurredAt);
    }

    private static void ValidateTransportHeaders(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreateAuditRecordCommand.IdempotencyKey), "Idempotency-Key is required.")
            ]);
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreateAuditRecordCommand.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            ]);
        }
    }

    private static Guid ResolveCorrelationId(
        HttpContext httpContext,
        string? correlationId,
        Guid? bodyCorrelationId)
    {
        if (bodyCorrelationId is { } validBodyCorrelationId && validBodyCorrelationId != Guid.Empty)
            return validBodyCorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId) && Guid.TryParse(correlationId, out Guid headerCorrelationId))
            return headerCorrelationId;

        string generated = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        return Guid.TryParse(generated, out Guid generatedCorrelationId)
            ? generatedCorrelationId
            : Guid.NewGuid();
    }
}
