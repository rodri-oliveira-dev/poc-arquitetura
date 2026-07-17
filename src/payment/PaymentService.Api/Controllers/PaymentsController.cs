using ApiDefaults.Middlewares;
using ApiDefaults.RateLimiting;

using Asp.Versioning;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PaymentService.Api.Contracts.Requests;
using PaymentService.Api.Contracts.Responses;
using PaymentService.Api.Controllers.Binds;
using PaymentService.Api.Mappers;
using PaymentService.Api.Security;
using PaymentService.Application.Payments.Queries;

using Swashbuckle.AspNetCore.Annotations;

namespace PaymentService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public sealed class PaymentsController(
    IMerchantAuthorizationService merchantAuthorizationService,
    ISender sender) : ControllerBase
{
    private readonly IMerchantAuthorizationService _merchantAuthorizationService = merchantAuthorizationService;
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Policy = ScopePolicies.PaymentWritePolicy)]
    [EnableRateLimiting(ApiRateLimitPolicies.AuthenticatedWrite)]
    [SwaggerOperation(
        OperationId = "CreatePayment",
        Summary = "Cria um payment externo.",
        Description = "Cria um Payment interno e solicita a criacao de uma intencao externa no provider configurado. A resposta sincrona nao confirma efeito financeiro final nem executa Ledger.")]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Payment aceito para confirmacao assincrona.", typeof(CreatePaymentResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito de idempotencia.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "Body acima do limite configurado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status502BadGateway, "Provider externo recusou a operacao.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Provider externo indisponivel.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status504GatewayTimeout, "Timeout com resultado externo desconhecido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<CreatePaymentResponse>> Create(
        [SwaggerParameter(Description = "Chave de idempotencia em formato UUID. Deve ser unica por operacao logica.")]
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = CreatePaymentBind.Bind(HttpContext, idempotencyKey, correlationId, request);

        if (!_merchantAuthorizationService.IsAuthorized(User, command.MerchantId))
            return Forbid();

        var result = await _sender.Send(command, cancellationToken);
        var response = PaymentMapper.ToCreateResponse(result);

        return Accepted(response.StatusUrl, response);
    }

    [HttpPost("{paymentId:guid}/refunds")]
    [Authorize(Policy = ScopePolicies.PaymentRefundPolicy)]
    [EnableRateLimiting(ApiRateLimitPolicies.AuthenticatedWrite)]
    [SwaggerOperation(
        OperationId = "RequestPaymentRefund",
        Summary = "Solicita refund de um payment.",
        Description = "Registra uma solicitacao de refund, chama o provider configurado de forma idempotente e conclui o efeito financeiro interno de forma assincrona por webhook e estorno no Ledger. Refund parcial e rejeitado neste MVP porque o contrato atual do Ledger suporta apenas estorno total.")]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Refund aceito para processamento assincrono.", typeof(RequestRefundResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Payment inexistente.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito de idempotencia ou refund pendente.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status502BadGateway, "Provider externo recusou a operacao.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status503ServiceUnavailable, "Provider externo indisponivel.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status504GatewayTimeout, "Timeout com resultado externo desconhecido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<RequestRefundResponse>> RequestRefund(
        [SwaggerParameter(Description = "Identificador do Payment.")]
        [FromRoute] Guid paymentId,
        [SwaggerParameter(Description = "Chave de idempotencia em formato UUID. Deve ser unica por operacao logica de refund.")]
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        [FromBody] RequestRefundRequest request,
        CancellationToken cancellationToken)
    {
        var authorizedMerchantIds = _merchantAuthorizationService.GetAuthorizedMerchantIds(User);
        if (authorizedMerchantIds.Count == 0)
            return Forbid();

        var command = RequestRefundBind.Bind(
            HttpContext,
            paymentId,
            idempotencyKey,
            correlationId,
            request,
            authorizedMerchantIds);

        var result = await _sender.Send(command, cancellationToken);
        var response = PaymentMapper.ToRefundResponse(result);

        return Accepted(response.StatusUrl, response);
    }

    [HttpGet("{paymentId:guid}")]
    [Authorize(Policy = ScopePolicies.PaymentReadPolicy)]
    [EnableRateLimiting(ApiRateLimitPolicies.AuthenticatedRead)]
    [SwaggerOperation(
        OperationId = "GetPaymentById",
        Summary = "Consulta um payment.",
        Description = "Retorna o estado interno do Payment registrado no PaymentService.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Payment encontrado.", typeof(PaymentResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Payment inexistente.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<PaymentResponse>> GetById(
        [SwaggerParameter(Description = "Identificador do Payment.")]
        [FromRoute] Guid paymentId,
        CancellationToken cancellationToken)
    {
        var authorizedMerchantIds = _merchantAuthorizationService.GetAuthorizedMerchantIds(User);
        if (authorizedMerchantIds.Count == 0)
            return Forbid();

        var result = await _sender.Send(
            new GetPaymentByIdQuery(paymentId, authorizedMerchantIds),
            cancellationToken);

        return Ok(PaymentMapper.ToResponse(result));
    }
}
