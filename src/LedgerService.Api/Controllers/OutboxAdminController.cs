using Asp.Versioning;
using LedgerService.Api.Contracts;
using LedgerService.Api.Security;
using LedgerService.Application.Outbox.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/outbox")]
public sealed class OutboxAdminController : ControllerBase
{
    private const int DefaultLimit = 50;
    private readonly ISender _sender;

    public OutboxAdminController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("failed/requeue")]
    [Authorize(Policy = ScopePolicies.OutboxRequeuePolicy)]
    [SwaggerOperation(
        Summary = "Recoloca mensagens Outbox Failed na fila de publicacao.",
        Description = "Fluxo administrativo protegido para recuperar mensagens Outbox que excederam MaxAttempts. Apenas mensagens Failed sao alteradas; mensagens Sent, Pending ou Processing sao ignoradas.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Mensagens elegiveis recolocadas como Pending.", typeof(RequeueFailedOutboxMessagesResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente para requeue de Outbox.")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<RequeueFailedOutboxMessagesResponse>> RequeueFailed(
        [FromBody] RequeueFailedOutboxMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RequeueFailedOutboxMessagesCommand(
                request.OutboxMessageId,
                request.EventType,
                request.OccurredFrom,
                request.OccurredUntil,
                request.Limit ?? DefaultLimit,
                request.Reason ?? string.Empty,
                GetOperator()),
            cancellationToken);

        return Ok(new RequeueFailedOutboxMessagesResponse
        {
            RequeuedCount = result.RequeuedCount,
            OutboxMessageIds = result.OutboxMessageIds
        });
    }

    private string GetOperator()
        => User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";
}
