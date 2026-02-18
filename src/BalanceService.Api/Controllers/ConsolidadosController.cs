using System.Diagnostics;
using System.Globalization;

using Asp.Versioning;

using BalanceService.Api.Contracts;
using BalanceService.Api.Middlewares;
using BalanceService.Api.Security;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Services;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BalanceService.Api.Controllers;

/// <summary>
/// Endpoints de consulta do banco consolidado (derivados da tabela <c>daily_balances</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/consolidados")]
public sealed class ConsolidadosController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.Api");

    private readonly IDailyBalanceService _dailyBalanceService;
    private readonly IPeriodBalanceService _periodBalanceService;
    private readonly IValidator<GetDailyBalanceQuery> _dailyValidator;
    private readonly IValidator<GetPeriodBalanceQuery> _periodValidator;
    private readonly ILogger<ConsolidadosController> _logger;

    public ConsolidadosController(
        IDailyBalanceService dailyBalanceService,
        IPeriodBalanceService periodBalanceService,
        IValidator<GetDailyBalanceQuery> dailyValidator,
        IValidator<GetPeriodBalanceQuery> periodValidator,
        ILogger<ConsolidadosController> logger)
    {
        _dailyBalanceService = dailyBalanceService;
        _periodBalanceService = periodBalanceService;
        _dailyValidator = dailyValidator;
        _periodValidator = periodValidator;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o consolidado diário para um <c>merchantId</c> e uma data.
    /// </summary>
    /// <remarks>
    /// Fonte: tabela <c>daily_balances</c>.
    /// 
    /// <para><b>Padrão quando não há dados</b>: retorna <b>200</b> com totais zerados.</para>
    /// </remarks>
    /// <param name="date">Data no path (formato <c>YYYY-MM-DD</c>).</param>
    /// <param name="merchantId">Identificador do merchant/lojista (obrigatório).</param>
    /// <param name="correlationId">Header opcional para correlação (<c>X-Correlation-Id</c>). Se ausente, a API gera.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">
    /// Consolidado diário encontrado (ou zeros quando não houver dados).
    /// Exemplo:
    /// <code>
    /// {
    ///   "merchantId": "tese",
    ///   "date": "2026-02-14",
    ///   "currency": "BRL",
    ///   "totalCredits": "150.00",
    ///   "totalDebits": "0.00",
    ///   "netBalance": "150.00",
    ///   "asOf": "2026-02-14T21:56:03.8825245-03:00",
    ///   "calculatedAt": "2026-02-15T10:00:00-03:00"
    /// }
    /// </code>
    /// </response>
    /// <response code="400">Parâmetros inválidos (ex.: data fora do formato, merchantId vazio, etc.).</response>
    /// <response code="401">Não autenticado (não configurado nesta POC, mas documentado para compatibilidade).</response>
    /// <response code="403">Sem permissão (não configurado nesta POC, mas documentado para compatibilidade).</response>
    /// <response code="500">Erro interno.</response>
    [HttpGet("diario/{date}")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [ProducesResponseType(typeof(DailyBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DailyBalanceResponse>> GetDaily(
        [FromRoute] string date,
        [FromQuery] string merchantId,
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            throw new ValidationException("date must be in format YYYY-MM-DD.");

        var query = new GetDailyBalanceQuery(merchantId, parsedDate);
        await _dailyValidator.ValidateAndThrowAsync(query, cancellationToken);

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MerchantId"] = merchantId,
            ["Date"] = parsedDate.ToString("yyyy-MM-dd"),
            ["CorrelationId"] = HttpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
        });

        using var activity = ActivitySource.StartActivity("balance.api.daily", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.date", parsedDate.ToString("yyyy-MM-dd"));

        var result = await _dailyBalanceService.GetDailyAsync(query, cancellationToken);

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

    /// <summary>
    /// Retorna o consolidado por período (totais + itens diários) para um <c>merchantId</c>.
    /// </summary>
    /// <remarks>
    /// Fonte: tabela <c>daily_balances</c>.
    /// 
    /// <para><b>Padrão quando não há dados</b>: retorna <b>200</b> com totais zerados e lista vazia.</para>
    /// </remarks>
    /// <param name="from">Data inicial (formato <c>YYYY-MM-DD</c>).</param>
    /// <param name="to">Data final (formato <c>YYYY-MM-DD</c>).</param>
    /// <param name="merchantId">Identificador do merchant/lojista (obrigatório).</param>
    /// <param name="correlationId">Header opcional para correlação (<c>X-Correlation-Id</c>). Se ausente, a API gera.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">
    /// Consolidado por período encontrado (ou zeros/lista vazia quando não houver dados).
    /// Exemplo:
    /// <code>
    /// {
    ///   "merchantId": "tese",
    ///   "from": "2026-02-10",
    ///   "to": "2026-02-14",
    ///   "currency": "BRL",
    ///   "totalCredits": "150.00",
    ///   "totalDebits": "20.00",
    ///   "netBalance": "130.00",
    ///   "items": [
    ///     { "date": "2026-02-10", "totalCredits": "0.00", "totalDebits": "20.00", "netBalance": "-20.00", "asOf": "2026-02-10T20:00:00-03:00" },
    ///     { "date": "2026-02-14", "totalCredits": "150.00", "totalDebits": "0.00", "netBalance": "150.00", "asOf": "2026-02-14T21:56:03.8825245-03:00" }
    ///   ],
    ///   "calculatedAt": "2026-02-15T10:00:00-03:00"
    /// }
    /// </code>
    /// </response>
    /// <response code="400">Parâmetros inválidos (ex.: from/to fora do formato, from &gt; to, merchantId vazio, etc.).</response>
    /// <response code="401">Não autenticado (não configurado nesta POC, mas documentado para compatibilidade).</response>
    /// <response code="403">Sem permissão (não configurado nesta POC, mas documentado para compatibilidade).</response>
    /// <response code="500">Erro interno.</response>
    [HttpGet("periodo")]
    [Authorize(Policy = ScopePolicies.BalanceReadPolicy)]
    [ProducesResponseType(typeof(PeriodBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PeriodBalanceResponse>> GetPeriod(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string merchantId,
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
            throw new ValidationException("from must be in format YYYY-MM-DD.");

        if (!DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
            throw new ValidationException("to must be in format YYYY-MM-DD.");

        var query = new GetPeriodBalanceQuery(merchantId, parsedFrom, parsedTo);
        await _periodValidator.ValidateAndThrowAsync(query, cancellationToken);

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MerchantId"] = merchantId,
            ["From"] = parsedFrom.ToString("yyyy-MM-dd"),
            ["To"] = parsedTo.ToString("yyyy-MM-dd"),
            ["CorrelationId"] = HttpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
        });

        using var activity = ActivitySource.StartActivity("balance.api.period", ActivityKind.Server);
        activity?.SetTag("balance.merchant_id", merchantId);
        activity?.SetTag("balance.from", parsedFrom.ToString("yyyy-MM-dd"));
        activity?.SetTag("balance.to", parsedTo.ToString("yyyy-MM-dd"));

        var result = await _periodBalanceService.GetPeriodAsync(query, cancellationToken);

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
}
