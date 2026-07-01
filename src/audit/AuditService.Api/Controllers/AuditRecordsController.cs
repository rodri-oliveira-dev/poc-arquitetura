using System.ComponentModel.DataAnnotations;

using ApiDefaults.Middlewares;

using Asp.Versioning;

using AuditService.Api.Contracts;
using AuditService.Api.Controllers.Binds;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace AuditService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-records")]
public sealed class AuditRecordsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [SwaggerOperation(
        OperationId = "CreateAuditRecord",
        Summary = "Cria um registro de auditoria funcional.",
        Description = "Cria um registro canonico e agnostico de auditoria funcional. O endpoint exige Idempotency-Key em formato UUID.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Registro de auditoria criado.", typeof(CreateAuditRecordResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito de idempotencia.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<CreateAuditRecordResponse>> Create(
        [SwaggerParameter(Description = "Chave de idempotencia em formato UUID.")]
        [Required]
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [SwaggerParameter(Description = "Correlation id opcional em formato UUID. Se ausente, a API usa correlationId do body ou o valor gerado pelo middleware.")]
        [FromHeader(Name = CorrelationIdMiddleware.HeaderName)] string? correlationId,
        [FromBody] CreateAuditRecordRequest request,
        CancellationToken cancellationToken)
    {
        var command = CreateAuditRecordBind.Bind(
            HttpContext,
            idempotencyKey,
            correlationId,
            request);

        var result = await _sender.Send(command, cancellationToken);
        var response = new CreateAuditRecordResponse(result.Id);

        return Created($"/api/v1/audit-records/{response.Id}", response);
    }
}
