using System.Security.Claims;

using ApiDefaults.RateLimiting;

using Asp.Versioning;

using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Api.Security;
using LedgerService.Application.Outbox.Commands;
using LedgerService.Application.Outbox.Queries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/outbox")]
[EnableRateLimiting(ApiRateLimitPolicies.Administrative)]
public sealed class OutboxAdminController(ISender sender) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private readonly ISender _sender = sender;

    [HttpGet("dead-letters")]
    [Authorize(Policy = ScopePolicies.OutboxAdminPolicy)]
    [SwaggerOperation(
        OperationId = "GetOutboxDeadLetters",
        Summary = "Lista mensagens Outbox em Dead Letter.",
        Description = "Fluxo administrativo protegido para inspeção paginada de mensagens Outbox que excederam o limite de retries.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Mensagens DeadLetter retornadas.", typeof(GetDeadLettersResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente para administrar Outbox.")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<GetDeadLettersResponse>> GetDeadLetters(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetDeadLettersQuery(page ?? DefaultPage, pageSize ?? DefaultPageSize),
            cancellationToken);

        return Ok(new GetDeadLettersResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = [.. result.Items.Select(x => new DeadLetterOutboxMessageResponse
            {
                Id = x.Id,
                AggregateType = x.AggregateType,
                AggregateId = x.AggregateId,
                EventType = x.EventType,
                OccurredAt = x.OccurredAt,
                RetryCount = x.RetryCount,
                LastError = x.LastError,
                CorrelationId = x.CorrelationId,
                TraceParent = x.TraceParent
            })]
        });
    }

    [HttpPost("dead-letters/{id:guid}/requeue")]
    [Authorize(Policy = ScopePolicies.OutboxAdminPolicy)]
    [SwaggerOperation(
        OperationId = "RequeueOutboxDeadLetter",
        Summary = "Recoloca uma mensagem Outbox DeadLetter na fila de publicacao.",
        Description = "Fluxo administrativo protegido para recuperar uma mensagem Outbox envenenada. Apenas mensagens DeadLetter sao alteradas.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Mensagem recolocada como Pending ou ignorada por nao estar em DeadLetter.", typeof(RequeueDeadLetterResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente para administrar Outbox.")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<RequeueDeadLetterResponse>> RequeueDeadLetter(
        Guid id,
        [FromBody] RequeueDeadLetterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(
            new RequeueDeadLetterCommand(
                id,
                request.Reason ?? string.Empty,
                GetOperator()),
            cancellationToken);

        return Ok(new RequeueDeadLetterResponse
        {
            Requeued = result.Requeued,
            OutboxMessageId = result.OutboxMessageId
        });
    }

    private string GetOperator()
        => User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";
}
