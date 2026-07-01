using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts.Responses;

[SwaggerSchema(Description = "Status de uma solicitacao de reprocessamento de lancamentos.")]
public sealed record ObterStatusReprocessamentoLancamentosResponse(
    [property: SwaggerSchema(Description = "Identificador da solicitacao de reprocessamento.")]
    Guid ReprocessamentoId,
    [property: SwaggerSchema(Description = "Merchant informado na solicitacao.")]
    string MerchantId,
    [property: SwaggerSchema(Description = "Data inicial inclusiva do periodo solicitado.")]
    DateOnly DataInicial,
    [property: SwaggerSchema(Description = "Data final inclusiva do periodo solicitado.")]
    DateOnly DataFinal,
    [property: SwaggerSchema(Description = "Estado atual da solicitacao.")]
    string Status,
    [property: SwaggerSchema(Description = "Motivo informado na solicitacao.")]
    string Motivo,
    [property: SwaggerSchema(Description = "Data e hora em que a solicitacao foi registrada.")]
    DateTime SolicitadoEm);
