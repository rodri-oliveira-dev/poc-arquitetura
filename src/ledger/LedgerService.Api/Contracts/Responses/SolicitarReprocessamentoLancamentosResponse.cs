using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts.Responses;

[SwaggerSchema(Description = "Resposta da solicitacao de reprocessamento aceita para fluxo assincrono.")]
public sealed record SolicitarReprocessamentoLancamentosResponse(
    [property: SwaggerSchema(Description = "Identificador da solicitacao de reprocessamento.")]
    Guid ReprocessamentoId,
    [property: SwaggerSchema(Description = "Merchant informado na solicitacao.")]
    string MerchantId,
    [property: SwaggerSchema(Description = "Data inicial inclusiva do periodo solicitado.")]
    DateOnly DataInicial,
    [property: SwaggerSchema(Description = "Data final inclusiva do periodo solicitado.")]
    DateOnly DataFinal,
    [property: SwaggerSchema(Description = "Status inicial da solicitacao. Valor esperado na criacao: Pending.")]
    string Status,
    [property: SwaggerSchema(Description = "URI para consulta do status da solicitacao.")]
    string StatusUrl);
