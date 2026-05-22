using System.Globalization;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace BalanceService.Api.Swagger;

public sealed class ConsolidadosExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.MethodInfo.DeclaringType?.Name, "ConsolidadosController", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(context.MethodInfo.Name, "GetDaily", StringComparison.Ordinal))
        {
            ApplyDailyExamples(operation);
            return;
        }

        if (string.Equals(context.MethodInfo.Name, "GetPeriod", StringComparison.Ordinal))
        {
            ApplyPeriodExamples(operation);
        }
    }

    private static void ApplyDailyExamples(OpenApiOperation operation)
    {
        if (TryGetJsonResponse(operation, StatusCodes.Status200OK, out var successMediaType))
        {
            successMediaType.Example = new JsonObject
            {
                ["merchantId"] = "tese",
                ["date"] = "2026-02-14",
                ["currency"] = "BRL",
                ["totalCredits"] = "150.00",
                ["totalDebits"] = "0.00",
                ["netBalance"] = "150.00",
                ["asOf"] = "2026-02-14T21:56:03.8825245-03:00",
                ["calculatedAt"] = "2026-02-15T10:00:00-03:00"
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status400BadRequest, out var errorMediaType))
        {
            errorMediaType.Example = CreateValidationErrorExample("date", "date must be in format YYYY-MM-DD.");
        }
    }

    private static void ApplyPeriodExamples(OpenApiOperation operation)
    {
        if (TryGetJsonResponse(operation, StatusCodes.Status200OK, out var successMediaType))
        {
            successMediaType.Example = new JsonObject
            {
                ["merchantId"] = "tese",
                ["from"] = "2026-02-10",
                ["to"] = "2026-02-14",
                ["currency"] = "BRL",
                ["totalCredits"] = "150.00",
                ["totalDebits"] = "20.00",
                ["netBalance"] = "130.00",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["date"] = "2026-02-10",
                        ["totalCredits"] = "0.00",
                        ["totalDebits"] = "20.00",
                        ["netBalance"] = "-20.00",
                        ["asOf"] = "2026-02-10T20:00:00-03:00"
                    },
                    new JsonObject
                    {
                        ["date"] = "2026-02-14",
                        ["totalCredits"] = "150.00",
                        ["totalDebits"] = "0.00",
                        ["netBalance"] = "150.00",
                        ["asOf"] = "2026-02-14T21:56:03.8825245-03:00"
                    }
                },
                ["calculatedAt"] = "2026-02-15T10:00:00-03:00"
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status400BadRequest, out var errorMediaType))
        {
            errorMediaType.Example = CreateValidationErrorExample("from", "from must be in format YYYY-MM-DD.");
        }
    }

    private static JsonObject CreateValidationErrorExample(string fieldName, string message)
    {
        return new JsonObject
        {
            ["type"] = "https://httpstatuses.com/400",
            ["title"] = "Invalid request",
            ["status"] = StatusCodes.Status400BadRequest,
            ["detail"] = "One or more validation errors occurred.",
            ["errors"] = new JsonObject
            {
                [fieldName] = new JsonArray
                {
                    message
                }
            },
            ["correlationId"] = "5b7f7b2d-5bb5-49d2-918d-c5d87ff54e3d"
        };
    }

    private static bool TryGetJsonResponse(OpenApiOperation operation, int statusCode, out OpenApiMediaType mediaType)
    {
        mediaType = null!;

        if (!operation.Responses.TryGetValue(statusCode.ToString(CultureInfo.InvariantCulture), out var response))
            return false;

        return response.Content.TryGetValue("application/json", out mediaType!);
    }
}
