using System.Globalization;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

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
            ((OpenApiMediaType)requestMediaType).Example = new JsonObject
            {
                ["merchantId"] = "tese",
                ["type"] = "CREDIT",
                ["amount"] = 150.00d,
                ["description"] = "Venda do pedido 12345",
                ["externalReference"] = "order-12345"
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status201Created, out var createdMediaType))
        {
            createdMediaType.Example = new JsonObject
            {
                ["id"] = "lan_9f3a1b2c",
                ["merchantId"] = "tese",
                ["type"] = "CREDIT",
                ["amount"] = "150.00",
                ["occurredAt"] = "2026-02-14T21:56:03.8825245-03:00",
                ["description"] = "Venda do pedido 12345",
                ["externalReference"] = "order-12345",
                ["createdAt"] = "2026-02-14T21:56:04.1023456-03:00"
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status400BadRequest, out var badRequestMediaType))
        {
            badRequestMediaType.Example = new JsonObject
            {
                ["type"] = "https://httpstatuses.com/400",
                ["title"] = "Invalid request",
                ["status"] = StatusCodes.Status400BadRequest,
                ["detail"] = "One or more validation errors occurred.",
                ["errors"] = new JsonObject
                {
                    ["amount"] = new JsonArray
                    {
                        "Amount must have at most 18 digits and 2 decimal places."
                    }
                },
                ["correlationId"] = "5b7f7b2d-5bb5-49d2-918d-c5d87ff54e3d"
            };
        }
    }

    private static bool TryGetJsonResponse(OpenApiOperation operation, int statusCode, out OpenApiMediaType mediaType)
    {
        mediaType = null!;

        if (!operation.Responses.TryGetValue(statusCode.ToString(CultureInfo.InvariantCulture), out var response))
            return false;

        return response.Content.TryGetValue("application/json", out mediaType!);
    }
}
