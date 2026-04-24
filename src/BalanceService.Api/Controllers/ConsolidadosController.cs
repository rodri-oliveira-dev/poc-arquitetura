using System.Diagnostics;

using Asp.Versioning;

using BalanceService.Api.Contracts;
using BalanceService.Api.Mappers;
using BalanceService.Api.Middlewares;
using BalanceService.Api.Security;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace BalanceService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/consolidados")]
public sealed class ConsolidadosController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Api");

    private readonly ISender _sender;
    private readonly IMerchantAuthorizationService _merchantAuthorizationService;

    public ConsolidadosController(
        ISender sender,
        IMerchantAuthorizationService merchantAuthorizationService)
    {
        _sender = sender;
        _merchantAuthorizationService = merchantAuthorizationService;
    }

    [HttpGet("diario/{date}")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [SwaggerOperation(
        Summary = "Consulta o consolidado diário.",
        Description = "Retorna o consolidado diário derivado de `daily_balances` para um `merchantId` e uma data. Quando não há dados, a API responde `200` com totais zerados.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Consolidado diário encontrado.", typeof(DailyBalanceResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Parâmetros inválidos.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou inválido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant informado.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<DailyBalanceResponse>> GetDaily(
        [SwaggerParameter(Description = "Data no path no formato `YYYY-MM-DD`.")]
        [FromRoute] string date,
        [SwaggerParameter(Description = "Identificador do merchant/lojista.")]
        [FromQuery] string merchantId,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var query = BalanceQueryMapper.ToDailyQuery(merchantId, date);

        if (!_merchantAuthorizationService.IsAuthorized(User, query.MerchantId))
            return Forbid();

        using var activity = ActivitySource.StartActivity("balance.api.daily", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.date", query.Date.ToString("yyyy-MM-dd"));

        var result = await _sender.Send(query, cancellationToken);
        return Ok(BalanceResponseMapper.ToResponse(result, DateTimeOffset.UtcNow));
    }

    [HttpGet("periodo")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [SwaggerOperation(
        Summary = "Consulta o consolidado por período.",
        Description = "Retorna o agregado do período e os itens diários derivados de `daily_balances`. Quando não há dados, a API responde `200` com totais zerados e lista vazia.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Consolidado por período encontrado.", typeof(PeriodBalanceResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Parâmetros inválidos.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou inválido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant informado.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<PeriodBalanceResponse>> GetPeriod(
        [SwaggerParameter(Description = "Data inicial no formato `YYYY-MM-DD`.")]
        [FromQuery] string from,
        [SwaggerParameter(Description = "Data final no formato `YYYY-MM-DD`.")]
        [FromQuery] string to,
        [SwaggerParameter(Description = "Identificador do merchant/lojista.")]
        [FromQuery] string merchantId,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var query = BalanceQueryMapper.ToPeriodQuery(merchantId, from, to);

        if (!_merchantAuthorizationService.IsAuthorized(User, query.MerchantId))
            return Forbid();

        using var activity = ActivitySource.StartActivity("balance.api.period", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.from", query.From.ToString("yyyy-MM-dd"));
        activity?.SetTag("balance.to", query.To.ToString("yyyy-MM-dd"));

        var result = await _sender.Send(query, cancellationToken);
        return Ok(BalanceResponseMapper.ToResponse(result, DateTimeOffset.UtcNow));
    }
}
