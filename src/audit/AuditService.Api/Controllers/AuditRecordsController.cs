using System.ComponentModel.DataAnnotations;

using ApiDefaults.Middlewares;

using Asp.Versioning;

using AuditService.Api.Contracts;
using AuditService.Api.Controllers.Binds;
using AuditService.Api.Security;
using AuditService.Application.FunctionalAuditing.GetAuditRecordById;
using AuditService.Application.FunctionalAuditing.GetAuditRecordsByOperationId;
using AuditService.Application.FunctionalAuditing.ReadModels;
using AuditService.Application.FunctionalAuditing.SearchAuditRecords;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace AuditService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-records")]
public sealed class AuditRecordsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuditScopePolicies.AuditRead)]
    [SwaggerOperation(
        OperationId = "GetAuditRecordById",
        Summary = "Consulta um registro de auditoria funcional por id.",
        Description = "Retorna um registro de auditoria funcional sem expor payload bruto sensivel.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Registro de auditoria encontrado.", typeof(AuditRecordResponse))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Registro de auditoria nao encontrado.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token JWT ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Token sem scope ou merchant autorizado para o registro.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<AuditRecordResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        AuditRecordReadModel? result = await _sender.Send(new GetAuditRecordByIdQuery(id), cancellationToken);

        return result is null
            ? NotFound()
            : User.CanAccessMerchant(result.MerchantId)
            ? Ok(ToResponse(result))
            : Forbid();
    }

    [HttpGet("operations/{operationId}")]
    [Authorize(Policy = AuditScopePolicies.AuditRead)]
    [SwaggerOperation(
        OperationId = "GetAuditRecordsByOperationId",
        Summary = "Consulta a trilha funcional de uma operacao.",
        Description = "Retorna registros da operacao ordenados por occurredAt asc para reconstruir a trilha funcional em ordem cronologica.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Registros de auditoria da operacao.", typeof(IReadOnlyCollection<AuditRecordResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token JWT ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Token sem scope autorizado.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyCollection<AuditRecordResponse>>> GetByOperationId(
        [FromRoute] string operationId,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<AuditRecordReadModel> results = await _sender.Send(
            new GetAuditRecordsByOperationIdQuery(operationId),
            cancellationToken);

        var authorizedResults = User.IsAuditAdmin()
            ? results
            : [.. results.Where(result => User.CanAccessMerchant(result.MerchantId))];

        return Ok(authorizedResults.Select(ToResponse).ToArray());
    }

    [HttpGet]
    [Authorize(Policy = AuditScopePolicies.AuditRead)]
    [SwaggerOperation(
        OperationId = "SearchAuditRecords",
        Summary = "Pesquisa registros de auditoria funcional.",
        Description = "Pesquisa registros de auditoria com filtros opcionais, periodo obrigatorio, paginacao segura e ordenacao padrao por occurredAt desc.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Pagina de registros de auditoria.", typeof(PagedResponse<AuditRecordResponse>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token JWT ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Token sem scope ou merchant autorizado para a consulta.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Request invalido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisicoes excedido.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Erro interno.", typeof(ProblemDetails))]
    public async Task<ActionResult<PagedResponse<AuditRecordResponse>>> Search(
        [FromQuery(Name = "merchantId")] string? merchantId,
        [FromQuery(Name = "sourceService")] string? sourceService,
        [FromQuery(Name = "operationType")] string? operationType,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "entityType")] string? entityType,
        [FromQuery(Name = "entityId")] string? entityId,
        [FromQuery(Name = "from")] DateTimeOffset? from,
        [FromQuery(Name = "to")] DateTimeOffset? to,
        [FromQuery(Name = "page")] int page = SearchAuditRecordsQuery.DefaultPage,
        [FromQuery(Name = "pageSize")] int pageSize = SearchAuditRecordsQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        string? authorizedMerchantId = ResolveAuthorizedMerchantId(merchantId);
        if (User.Identity?.IsAuthenticated == true && !User.IsAuditAdmin() && authorizedMerchantId is null)
            return Forbid();

        PagedResult<AuditRecordReadModel> result = await _sender.Send(
            new SearchAuditRecordsQuery(
                User.IsAuditAdmin() ? merchantId : authorizedMerchantId,
                sourceService,
                operationType,
                status,
                entityType,
                entityId,
                from,
                to,
                page,
                pageSize),
            cancellationToken);

        return Ok(new PagedResponse<AuditRecordResponse>(
            [.. result.Items.Select(ToResponse)],
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages));
    }

    [HttpPost]
    [Authorize(Policy = AuditScopePolicies.AuditWrite)]
    [SwaggerOperation(
        OperationId = "CreateAuditRecord",
        Summary = "Cria um registro de auditoria funcional.",
        Description = "Cria um registro canonico e agnostico de auditoria funcional. O endpoint exige Idempotency-Key em formato UUID.")]
    [SwaggerResponse(StatusCodes.Status201Created, "Registro de auditoria criado.", typeof(CreateAuditRecordResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Token JWT ausente ou invalido.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Token sem scope audit.write.")]
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

    private static AuditRecordResponse ToResponse(AuditRecordReadModel model)
        => new(
            model.Id,
            model.OperationId,
            model.CorrelationId,
            model.SourceService,
            model.OperationType,
            model.EntityType,
            model.EntityId,
            model.MerchantId,
            model.Actor is null
                ? null
                : new AuditRecordActorResponse(model.Actor.Type, model.Actor.Subject, model.Actor.ClientId),
            model.Status,
            model.Reason,
            model.Metadata,
            model.OccurredAt,
            model.CreatedAt);

    private string? ResolveAuthorizedMerchantId(string? requestedMerchantId)
    {
        if (User.IsAuditAdmin())
            return requestedMerchantId;

        IReadOnlyCollection<string> merchantIds = User.MerchantIds();
        return !string.IsNullOrWhiteSpace(requestedMerchantId)
            ? merchantIds.Contains(requestedMerchantId, StringComparer.Ordinal) ? requestedMerchantId : null
            : merchantIds.Count == 1 ? merchantIds.Single() : null;
    }
}
