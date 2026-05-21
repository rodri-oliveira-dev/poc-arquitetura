using Swashbuckle.AspNetCore.Annotations;

namespace LedgerService.Api.Contracts.Requests;

[SwaggerSchema(Description = "Payload para solicitar estorno assincrono de um lancamento.")]
public sealed record SolicitarEstornoLancamentoRequest(
    [property: SwaggerSchema(Description = "Motivo operacional ou de negocio para solicitar o estorno.")]
    string Motivo);
