using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts.Responses;

[SwaggerSchema(Description = "Resposta da consulta de status de uma solicitacao de estorno.")]
public sealed record ObterStatusEstornoLancamentoResponse(
    [property: SwaggerSchema(Description = "Identificador da solicitacao de estorno.")]
    Guid EstornoId,
    [property: SwaggerSchema(Description = "Identificador do lancamento original.")]
    Guid LancamentoOriginalId,
    [property: SwaggerSchema(Description = "Estado atual da solicitacao de estorno.")]
    string Status,
    [property: SwaggerSchema(Description = "Motivo informado na solicitacao de estorno.")]
    string Motivo,
    [property: SwaggerSchema(Description = "Data e hora em que a solicitacao foi registrada.")]
    DateTime SolicitadoEm);
