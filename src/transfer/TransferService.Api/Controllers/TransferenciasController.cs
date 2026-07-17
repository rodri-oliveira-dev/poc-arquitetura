using ApiDefaults.Middlewares;
using ApiDefaults.RateLimiting;

using Asp.Versioning;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Swashbuckle.AspNetCore.Annotations;

using TransferService.Api.Contracts.Requests;
using TransferService.Api.Contracts.Responses;
using TransferService.Api.Controllers.Binds;
using TransferService.Api.Mappers;
using TransferService.Api.Security;
using TransferService.Application.Transferencias.Queries;

namespace TransferService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/transferencias")]
public sealed class TransferenciasController(
    IMerchantAuthorizationService merchantAuthorizationService,
    ISender sender) : ControllerBase
{
    private readonly IMerchantAuthorizationService _merchantAuthorizationService = merchantAuthorizationService;
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Policy = ScopePolicies.TransferWritePolicy)]
    [EnableRateLimiting(ApiRateLimitPolicies.AuthenticatedWrite)]
    [SwaggerOperation(
        OperationId = "SolicitarTransferencia",
        Summary = "Solicita uma transferencia entre merchants.",
        Description = "Registra uma saga de transferencia com status Pending e grava a intencao no Outbox para processamento assincrono posterior. O endpoint exige Idempotency-Key e retorna 202 Accepted.")]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Solicitacao aceita para processamento assincrono. Retorna Location com a URI de status.", typeof(SolicitarTransferenciaResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant de origem.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito de idempotencia.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "Body acima do limite configurado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<SolicitarTransferenciaResponse>> Solicitar(
        [SwaggerParameter(Description = "Chave de idempotencia em formato UUID. Deve ser unica por operacao logica.")]
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        [FromBody] SolicitarTransferenciaRequest request,
        CancellationToken cancellationToken)
    {
        var command = SolicitarTransferenciaBind.Bind(
            HttpContext,
            idempotencyKey,
            correlationId,
            request);

        if (!_merchantAuthorizationService.IsAuthorized(User, command.SourceMerchantId))
            return Forbid();

        var result = await _sender.Send(command, cancellationToken);
        var response = TransferenciaMapper.ToResponse(result);

        return Accepted(response.StatusUrl, response);
    }

    [HttpGet("{transferenciaId:guid}")]
    [Authorize(Policy = ScopePolicies.TransferReadPolicy)]
    [EnableRateLimiting(ApiRateLimitPolicies.AuthenticatedRead)]
    [SwaggerOperation(
        OperationId = "ObterStatusTransferencia",
        Summary = "Consulta status de uma transferencia.",
        Description = "Retorna o estado atual da saga de transferencia registrada previamente. O processamento da transferencia e assincrono e pode evoluir apos a resposta do endpoint de criacao.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Status da transferencia.", typeof(ObterStatusTransferenciaResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para os merchants da transferencia.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Transferencia inexistente.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<ObterStatusTransferenciaResponse>> ObterStatus(
        [SwaggerParameter(Description = "Identificador da transferencia.")]
        [FromRoute] Guid transferenciaId,
        CancellationToken cancellationToken)
    {
        var authorizedMerchantIds = _merchantAuthorizationService.GetAuthorizedMerchantIds(User);
        if (authorizedMerchantIds.Count == 0)
            return Forbid();

        var result = await _sender.Send(
            new ObterStatusTransferenciaQuery(
                transferenciaId,
                authorizedMerchantIds),
            cancellationToken);

        return Ok(TransferenciaMapper.ToResponse(result));
    }
}
