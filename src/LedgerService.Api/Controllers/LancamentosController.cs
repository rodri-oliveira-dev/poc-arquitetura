using Asp.Versioning;
using LedgerService.Api.Contracts;
using LedgerService.Api.Controllers.Binds;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Security;
using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lancamentos")]
public sealed class LancamentosController : ControllerBase
{
    private readonly CreateLancamentoService _createLancamentoService;
    private readonly IMerchantAuthorizationService _merchantAuthorizationService;

    public LancamentosController(
        CreateLancamentoService createLancamentoService,
        IMerchantAuthorizationService merchantAuthorizationService)
    {
        _createLancamentoService = createLancamentoService;
        _merchantAuthorizationService = merchantAuthorizationService;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.LedgerWritePolicy)]
    [SwaggerOperation(
        Summary = "Cria um lançamento no ledger.",
        Description = "Registra um lançamento CREDIT ou DEBIT. O endpoint exige `Idempotency-Key`, aceita `X-Correlation-Id` opcional, aplica limite de body configurável por `ApiLimits:MaxRequestBodySizeBytes` e retorna `409` quando a mesma chave de idempotência é reutilizada com payload diferente.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Lançamento criado com sucesso. Retorna Location com a URI canônica do recurso.", typeof(LancamentoDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request inválido.", typeof(ValidationErrorResponse))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito de idempotência.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "Body acima do limite configurado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Violação de regra de domínio.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições excedido.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token ausente ou inválido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Scope insuficiente ou token sem autorizacao para o merchant informado.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<LancamentoDto>> Create(
        [SwaggerParameter(Description = "Chave de idempotência em formato UUID. Deve ser única por operação lógica.")]
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API gera e devolve um valor no response header.")]
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

        if (!_merchantAuthorizationService.IsAuthorized(User, validRequest.MerchantId))
            return Forbid();

        var created = await _createLancamentoService.ExecuteAsync(validRequest, cancellationToken);

        // Ainda não há endpoint GET por id; Location identifica a URI canônica futura do recurso criado.
        return Created($"{Request.Path}/{created.Id}", created);
    }
}
