using Swashbuckle.AspNetCore.Annotations;

namespace PaymentService.Api.Contracts.Responses;

[SwaggerSchema(Description = "Resposta da solicitacao de refund aceita para processamento assincrono.")]
public sealed record RequestRefundResponse(
    [property: SwaggerSchema(Description = "Identificador do Payment.")]
    Guid PaymentId,
    [property: SwaggerSchema(Description = "Identificador interno do refund.")]
    Guid RefundId,
    [property: SwaggerSchema(Description = "Estado interno do refund.")]
    string Status,
    [property: SwaggerSchema(Description = "Valor do refund.")]
    decimal Amount,
    [property: SwaggerSchema(Description = "Moeda ISO.")]
    string Currency,
    [property: SwaggerSchema(Description = "Motivo do refund.")]
    string Reason,
    [property: SwaggerSchema(Description = "Referencia externa opcional.")]
    string? ExternalReference,
    [property: SwaggerSchema(Description = "URI futura/estavel para consultar o Payment.")]
    string StatusUrl);
