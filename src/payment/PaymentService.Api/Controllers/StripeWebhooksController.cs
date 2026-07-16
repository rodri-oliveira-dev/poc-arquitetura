using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using ApiDefaults.Middlewares;
using ApiDefaults.RateLimiting;

using Asp.Versioning;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PaymentService.Api.Webhooks;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Payments.Webhooks;

using Swashbuckle.AspNetCore.Annotations;

namespace PaymentService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/stripe")]
[AllowAnonymous]
[EnableRateLimiting(ApiRateLimitPolicies.AnonymousWebhook)]
public sealed class StripeWebhooksController(
    StripeWebhookValidator validator,
    PaymentWebhookTelemetry telemetry,
    ISender sender,
    ILogger<StripeWebhooksController> logger) : ControllerBase
{
    private const string ProviderName = "Stripe";
    private const string StripeSignatureHeader = "Stripe-Signature";

    private static readonly Action<ILogger, string, string, string, Exception?> _logPersistFailure =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1, nameof(_logPersistFailure)),
            "Falha ao persistir webhook Stripe na Inbox. Provider={Provider} EventType={EventType} ProviderEventId={ProviderEventId}");

    private static readonly Action<ILogger, string, StripeWebhookValidationFailure, Exception?> _logRejected =
        LoggerMessage.Define<string, StripeWebhookValidationFailure>(
            LogLevel.Warning,
            new EventId(2, nameof(_logRejected)),
            "Webhook Stripe rejeitado. Provider={Provider} Reason={Reason}");

    private static readonly Action<ILogger, string, string, string, Exception?> _logDuplicate =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(3, nameof(_logDuplicate)),
            "Webhook Stripe duplicado reconhecido. Provider={Provider} EventType={EventType} ProviderEventId={ProviderEventId}");

    private static readonly Action<ILogger, string, string, string, StripeWebhookEventCategory, Exception?> _logIgnored =
        LoggerMessage.Define<string, string, string, StripeWebhookEventCategory>(
            LogLevel.Information,
            new EventId(4, nameof(_logIgnored)),
            "Webhook Stripe persistido como ignorado. Provider={Provider} EventType={EventType} ProviderEventId={ProviderEventId} Category={Category}");

    private static readonly Action<ILogger, string, string, string, Exception?> _logPersisted =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(5, nameof(_logPersisted)),
            "Webhook Stripe persistido na Inbox. Provider={Provider} EventType={EventType} ProviderEventId={ProviderEventId}");

    private readonly StripeWebhookValidator _validator = validator;
    private readonly PaymentWebhookTelemetry _telemetry = telemetry;
    private readonly ISender _sender = sender;
    private readonly ILogger<StripeWebhooksController> _logger = logger;

    [HttpPost]
    [Consumes("application/json")]
    [SwaggerOperation(
        OperationId = "ReceiveStripeWebhook",
        Summary = "Recebe webhook Stripe.",
        Description = "Recebe o raw body enviado pela Stripe, valida o header Stripe-Signature e persiste o evento na Inbox antes de responder. Este endpoint nao usa JWT; a autenticidade e a assinatura criptografica do provider.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Evento persistido, ignorado de forma controlada ou duplicado reconhecido.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Header de assinatura ausente/malformado ou payload invalido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Assinatura invalida ou timestamp fora da tolerancia.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "Body acima do limite configurado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Webhook signing secret ausente ou Inbox indisponivel.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    [SuppressMessage("Major Code Smell", "S6932:Use model binding instead of accessing raw request data", Justification = "Stripe signature verification requires the exact raw body and Stripe-Signature header.")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var rawBody = await ReadRawBodyAsync(Request, cancellationToken);
        var signature = Request.Headers[StripeSignatureHeader].FirstOrDefault();

        StripeWebhookValidationResult validation;
        using (_telemetry.StartSignatureValidation())
        {
            validation = _validator.Validate(rawBody, signature);
        }

        if (!validation.IsValid)
            return Reject(validation.Failure ?? StripeWebhookValidationFailure.InvalidPayload);

        Debug.Assert(validation.ProviderEventId is not null);
        Debug.Assert(validation.EventType is not null);
        Debug.Assert(validation.RawPayload is not null);

        var command = new ReceiveStripeWebhookCommand(
            validation.ProviderEventId,
            validation.EventType,
            validation.RawPayload,
            Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault(),
            validation.ProviderPaymentId,
            validation.PaymentId);

        ReceiveStripeWebhookResult result;
        try
        {
            using (_telemetry.StartInboxPersist())
            {
                result = await _sender.Send(command, cancellationToken);
            }
        }
        catch
        {
            _telemetry.RecordPersistFailure();
            _logPersistFailure(
                _logger,
                ProviderName,
                validation.EventType,
                validation.ProviderEventId,
                null);
            throw;
        }

        RecordSuccess(result, validation.ProviderEventId);
        return Ok();
    }

    private ObjectResult Reject(StripeWebhookValidationFailure failure)
    {
        _telemetry.RecordInvalidSignature(failure);

        var statusCode = failure switch
        {
            StripeWebhookValidationFailure.MissingSecret => StatusCodes.Status503ServiceUnavailable,
            StripeWebhookValidationFailure.InvalidSignature or StripeWebhookValidationFailure.TimestampOutsideTolerance => StatusCodes.Status401Unauthorized,
            StripeWebhookValidationFailure.MissingSignatureHeader
                or StripeWebhookValidationFailure.MalformedSignatureHeader
                or StripeWebhookValidationFailure.InvalidPayload => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        var title = failure switch
        {
            StripeWebhookValidationFailure.MissingSignatureHeader => "Stripe signature header ausente",
            StripeWebhookValidationFailure.MissingSecret => "Webhook Stripe nao configurado",
            StripeWebhookValidationFailure.MalformedSignatureHeader => "Stripe signature header invalido",
            StripeWebhookValidationFailure.TimestampOutsideTolerance => "Stripe signature timestamp invalido",
            StripeWebhookValidationFailure.InvalidSignature => "Stripe signature invalida",
            StripeWebhookValidationFailure.InvalidPayload => "Payload Stripe invalido",
            _ => "Webhook Stripe invalido"
        };

        _logRejected(
            _logger,
            ProviderName,
            failure,
            null);

        return Problem(statusCode: statusCode, title: title);
    }

    private void RecordSuccess(ReceiveStripeWebhookResult result, string providerEventId)
    {
        var outcome = GetTelemetryOutcome(result);

        _telemetry.RecordReceived(outcome, result.EventCategory);

        if (result.StoreResult == PaymentInboxStoreResult.Duplicate)
        {
            _telemetry.RecordDuplicate(result.EventCategory);
            _logDuplicate(
                _logger,
                ProviderName,
                result.EventType,
                providerEventId,
                null);
            return;
        }

        if (result.InboxStatus == PaymentInboxStatus.Ignored)
        {
            _telemetry.RecordIgnored(result.EventCategory);
            _logIgnored(
                _logger,
                ProviderName,
                result.EventType,
                providerEventId,
                result.EventCategory,
                null);
            return;
        }

        _telemetry.RecordPending();
        _logPersisted(
            _logger,
            ProviderName,
            result.EventType,
            providerEventId,
            null);
    }

    private static string GetTelemetryOutcome(ReceiveStripeWebhookResult result)
    {
        if (result.StoreResult == PaymentInboxStoreResult.Duplicate)
            return "duplicate";

        return result.InboxStatus == PaymentInboxStatus.Ignored ? "ignored" : "persisted";
    }

    private static async Task<byte[]> ReadRawBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}
