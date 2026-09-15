using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentService.Api.Swagger;

public sealed class StripeWebhookOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(operation.OperationId, "ReceiveStripeWebhook", StringComparison.Ordinal))
            return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Stripe-Signature",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Assinatura enviada pela Stripe no formato t=<timestamp>,v1=<hmac>.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        });

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "Raw JSON body enviado pela Stripe. A assinatura deve ser calculada sobre estes bytes sem normalizacao.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                {
                    "application/json",
                    new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            AdditionalPropertiesAllowed = true
                        }
                    }
                }
            }
        };

        operation.Security = [];
    }
}
