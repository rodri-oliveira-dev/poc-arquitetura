using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace LedgerService.Api.Swagger;

public sealed class LancamentosExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.MethodInfo.DeclaringType?.Name, "LancamentosController", StringComparison.Ordinal) ||
            !string.Equals(context.MethodInfo.Name, "Create", StringComparison.Ordinal))
        {
            return;
        }

        if (operation.RequestBody?.Content.TryGetValue("application/json", out var requestMediaType) == true)
        {
            requestMediaType.Example = new OpenApiObject
            {
                ["merchantId"] = new OpenApiString("tese"),
                ["type"] = new OpenApiString("CREDIT"),
                ["amount"] = new OpenApiDouble(150.00d),
                ["description"] = new OpenApiString("Venda do pedido 12345"),
                ["externalReference"] = new OpenApiString("order-12345")
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status201Created, out var createdMediaType))
        {
            createdMediaType.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("lan_9f3a1b2c"),
                ["merchantId"] = new OpenApiString("tese"),
                ["type"] = new OpenApiString("CREDIT"),
                ["amount"] = new OpenApiString("150.00"),
                ["occurredAt"] = new OpenApiString("2026-02-14T21:56:03.8825245-03:00"),
                ["description"] = new OpenApiString("Venda do pedido 12345"),
                ["externalReference"] = new OpenApiString("order-12345"),
                ["createdAt"] = new OpenApiString("2026-02-14T21:56:04.1023456-03:00")
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status400BadRequest, out var badRequestMediaType))
        {
            badRequestMediaType.Example = new OpenApiObject
            {
                ["type"] = new OpenApiString("https://httpstatuses.com/400"),
                ["title"] = new OpenApiString("Invalid request"),
                ["status"] = new OpenApiInteger(StatusCodes.Status400BadRequest),
                ["detail"] = new OpenApiString("One or more validation errors occurred."),
                ["errors"] = new OpenApiObject
                {
                    ["amount"] = new OpenApiArray
                    {
                        new OpenApiString("Amount must be greater than zero for CREDIT.")
                    }
                },
                ["correlationId"] = new OpenApiString("5b7f7b2d-5bb5-49d2-918d-c5d87ff54e3d")
            };
        }
    }

    private static bool TryGetJsonResponse(OpenApiOperation operation, int statusCode, out OpenApiMediaType mediaType)
    {
        mediaType = null!;

        return operation.Responses.TryGetValue(statusCode.ToString(), out var response) &&
               response.Content.TryGetValue("application/json", out mediaType);
    }
}
