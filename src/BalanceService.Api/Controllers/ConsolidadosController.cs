using System.Diagnostics;
using System.Globalization;

using Asp.Versioning;

using BalanceService.Api.Contracts;
using BalanceService.Api.Middlewares;
using BalanceService.Api.Security;
using BalanceService.Application.Balances.Queries;

using FluentValidation;
using FluentValidation.Results;

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

    public ConsolidadosController(
        ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("diario/{date}")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [SwaggerOperation(
        Summary = "Consulta o consolidado diário.",
        Description = "Retorna o consolidado diário derivado de `daily_balances` para um `merchantId` e uma data. Quando não há dados, a API responde `200` com totais zerados.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Consolidado diário encontrado.", typeof(DailyBalanceResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Parâmetros inválidos.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou inválido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente para consultar o consolidado.")]
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
        var parsedDate = ParseDateOrThrow(date, nameof(date));

        var query = new GetDailyBalanceQuery(merchantId, parsedDate);

        using var activity = ActivitySource.StartActivity("balance.api.daily", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.date", parsedDate.ToString("yyyy-MM-dd"));

        var result = await _sender.Send(query, cancellationToken);

        // Contract: CalculatedAt deve refletir o momento da resposta.
        var calculatedAt = DateTimeOffset.UtcNow;
        var response = new DailyBalanceResponse
        {
            MerchantId = result.MerchantId,
            Date = result.Date.ToString("yyyy-MM-dd"),
            Currency = result.Currency,
            TotalCredits = result.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
            TotalDebits = result.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
            NetBalance = result.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
            AsOf = result.AsOf == DateTimeOffset.MinValue ? null : result.AsOf.ToString("o"),
            CalculatedAt = calculatedAt.ToString("o")
        };

        return Ok(response);
    }

    [HttpGet("periodo")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [SwaggerOperation(
        Summary = "Consulta o consolidado por período.",
        Description = "Retorna o agregado do período e os itens diários derivados de `daily_balances`. Quando não há dados, a API responde `200` com totais zerados e lista vazia.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Consolidado por período encontrado.", typeof(PeriodBalanceResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Parâmetros inválidos.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou inválido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente para consultar o consolidado.")]
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
        var parsedFrom = ParseDateOrThrow(from, nameof(from));
        var parsedTo = ParseDateOrThrow(to, nameof(to));

        var query = new GetPeriodBalanceQuery(merchantId, parsedFrom, parsedTo);

        using var activity = ActivitySource.StartActivity("balance.api.period", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.from", parsedFrom.ToString("yyyy-MM-dd"));
        activity?.SetTag("balance.to", parsedTo.ToString("yyyy-MM-dd"));

        var result = await _sender.Send(query, cancellationToken);

        // Contract: CalculatedAt deve refletir o momento da resposta.
        var calculatedAt = DateTimeOffset.UtcNow;

        var items = result.Items
            .Select(x => new PeriodBalanceItemResponse
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                TotalCredits = x.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
                TotalDebits = x.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
                NetBalance = x.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
                AsOf = x.AsOf == DateTimeOffset.MinValue ? null : x.AsOf.ToString("o")
            })
            .ToList();

        var response = new PeriodBalanceResponse
        {
            MerchantId = result.MerchantId,
            From = result.From.ToString("yyyy-MM-dd"),
            To = result.To.ToString("yyyy-MM-dd"),
            Currency = result.Currency,
            TotalCredits = result.TotalCredits.ToString("0.00", CultureInfo.InvariantCulture),
            TotalDebits = result.TotalDebits.ToString("0.00", CultureInfo.InvariantCulture),
            NetBalance = result.NetBalance.ToString("0.00", CultureInfo.InvariantCulture),
            Items = items,
            CalculatedAt = calculatedAt.ToString("o")
        };

        return Ok(response);
    }

    private static DateOnly ParseDateOrThrow(string rawValue, string parameterName)
    {
        if (DateOnly.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return parsedDate;

        throw new ValidationException(new[]
        {
            new ValidationFailure(parameterName, $"{parameterName} must be in format YYYY-MM-DD.")
        });
    }
}
