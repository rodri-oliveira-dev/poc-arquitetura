using Microsoft.AspNetCore.Mvc;
using LedgerService.Api.Contracts;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using LedgerService.Application.Lancamentos.Services;

namespace LedgerService.Api.Controllers;

[ApiController]
[Route("api/v1/lancamentos")]
public sealed class LancamentosController : ControllerBase
{
    private readonly CreateLancamentoService _createLancamentoService;

    public LancamentosController(CreateLancamentoService createLancamentoService)
    {
        _createLancamentoService = createLancamentoService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(LancamentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LancamentoDto>> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromHeader(Name = "X-Correlation-Id")] string correlationId,
        [FromBody] CreateLancamentoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateLancamentoInput(
            request.MerchantId,
            request.Type,
            request.Amount,
            request.Currency,
            request.OccurredAt,
            request.Description,
            request.ExternalReference,
            idempotencyKey,
            correlationId);

        var created = await _createLancamentoService.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}