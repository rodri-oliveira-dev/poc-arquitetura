using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts.Responses;

[SwaggerSchema(Description = "Resposta da solicitacao de estorno aceita para processamento assincrono.")]
public sealed record SolicitarEstornoLancamentoResponse(
    [property: SwaggerSchema(Description = "Identificador da solicitacao de estorno.")]
    Guid EstornoId,
    [property: SwaggerSchema(Description = "Identificador do lancamento original.")]
    Guid LancamentoOriginalId,
    [property: SwaggerSchema(Description = "Status inicial da solicitacao. Valor esperado na criacao: Pending.")]
    string Status,
    [property: SwaggerSchema(Description = "URI para consulta futura do status da solicitacao.")]
    string StatusUrl);
