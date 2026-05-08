using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts;

[SwaggerSchema(Description = "Payload para solicitar reprocessamento assincrono de lancamentos.")]
public sealed record SolicitarReprocessamentoLancamentosRequest(
    [property: SwaggerSchema(Description = "Merchant autorizado para o qual o periodo sera reprocessado.")]
    string MerchantId,
    [property: SwaggerSchema(Description = "Data inicial inclusiva do periodo a reprocessar.")]
    DateOnly DataInicial,
    [property: SwaggerSchema(Description = "Data final inclusiva do periodo a reprocessar.")]
    DateOnly DataFinal,
    [property: SwaggerSchema(Description = "Motivo operacional ou de negocio para solicitar o reprocessamento.")]
    string Motivo);
