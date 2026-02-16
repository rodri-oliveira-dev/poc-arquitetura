using Asp.Versioning;
using LedgerService.Api.Contracts;
using LedgerService.Api.Controllers.Binds;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Security;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Api.Controllers;

/// <summary>
/// Endpoints para criação de lançamentos no ledger.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lancamentos")]
public sealed class LancamentosController : ControllerBase
{
    private readonly CreateLancamentoService _createLancamentoService;

    public LancamentosController(CreateLancamentoService createLancamentoService)
    {
        _createLancamentoService = createLancamentoService;
    }

    /// <summary>
    /// Cria um lançamento (CREDIT ou DEBIT) com idempotência e correlação.
    /// </summary>
    /// <remarks>
    /// Regras (comprovadas no código):
    /// - <b>Idempotência</b>: o header <c>Idempotency-Key</c> deve ser um UUID.
    ///   - Se a mesma chave for usada com payload diferente, retorna 409 (Conflict).
    ///   - Se existir resposta armazenada para a mesma chave/payload, a API pode retornar replay da resposta.
    /// - <b>CorrelationId</b>: o header <c>X-Correlation-Id</c> é opcional; se ausente, a API gera um UUID e o retorna no response.
    /// - <b>Rate limit</b>: o endpoint está sujeito a rate limiting (HTTP 429).
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = ScopePolicies.LedgerWritePolicy)]
    [ProducesResponseType(typeof(LancamentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LancamentoDto>> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        [FromBody] CreateLancamentoRequest request,
        CancellationToken cancellationToken)
    {
        var validRequest = await CreateLancamentoBind.BindAsync(
            HttpContext,
            idempotencyKey,
            correlationId,
            request,
            cancellationToken);

        var created = await _createLancamentoService.ExecuteAsync(validRequest, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
