using Swashbuckle.AspNetCore.Annotations;

namespace PaymentService.Api.Contracts.Requests;

[SwaggerSchema(Description = "Payload para solicitar refund assincrono de um Payment.")]
public sealed record RequestRefundRequest(
    [property: SwaggerSchema(Description = "Valor do refund. Omitir para refund total. Refund parcial e rejeitado neste MVP.")]
    decimal? Amount,
    [property: SwaggerSchema(Description = "Motivo do refund. Ex.: requested_by_customer.")]
    string Reason,
    [property: SwaggerSchema(Description = "Referencia externa opcional da solicitacao de refund.")]
    string? ExternalReference);
