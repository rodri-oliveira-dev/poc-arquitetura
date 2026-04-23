using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

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
            successMediaType.Example = new OpenApiObject
            {
                ["merchantId"] = new OpenApiString("tese"),
                ["date"] = new OpenApiString("2026-02-14"),
                ["currency"] = new OpenApiString("BRL"),
                ["totalCredits"] = new OpenApiString("150.00"),
                ["totalDebits"] = new OpenApiString("0.00"),
                ["netBalance"] = new OpenApiString("150.00"),
                ["asOf"] = new OpenApiString("2026-02-14T21:56:03.8825245-03:00"),
                ["calculatedAt"] = new OpenApiString("2026-02-15T10:00:00-03:00")
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
            successMediaType.Example = new OpenApiObject
            {
                ["merchantId"] = new OpenApiString("tese"),
                ["from"] = new OpenApiString("2026-02-10"),
                ["to"] = new OpenApiString("2026-02-14"),
                ["currency"] = new OpenApiString("BRL"),
                ["totalCredits"] = new OpenApiString("150.00"),
                ["totalDebits"] = new OpenApiString("20.00"),
                ["netBalance"] = new OpenApiString("130.00"),
                ["items"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["date"] = new OpenApiString("2026-02-10"),
                        ["totalCredits"] = new OpenApiString("0.00"),
                        ["totalDebits"] = new OpenApiString("20.00"),
                        ["netBalance"] = new OpenApiString("-20.00"),
                        ["asOf"] = new OpenApiString("2026-02-10T20:00:00-03:00")
                    },
                    new OpenApiObject
                    {
                        ["date"] = new OpenApiString("2026-02-14"),
                        ["totalCredits"] = new OpenApiString("150.00"),
                        ["totalDebits"] = new OpenApiString("0.00"),
                        ["netBalance"] = new OpenApiString("150.00"),
                        ["asOf"] = new OpenApiString("2026-02-14T21:56:03.8825245-03:00")
                    }
                },
                ["calculatedAt"] = new OpenApiString("2026-02-15T10:00:00-03:00")
            };
        }

        if (TryGetJsonResponse(operation, StatusCodes.Status400BadRequest, out var errorMediaType))
        {
            errorMediaType.Example = CreateValidationErrorExample("from", "from must be in format YYYY-MM-DD.");
        }
    }

    private static OpenApiObject CreateValidationErrorExample(string fieldName, string message)
    {
        return new OpenApiObject
        {
            ["type"] = new OpenApiString("https://httpstatuses.com/400"),
            ["title"] = new OpenApiString("Invalid request"),
            ["status"] = new OpenApiInteger(StatusCodes.Status400BadRequest),
            ["detail"] = new OpenApiString("One or more validation errors occurred."),
            ["errors"] = new OpenApiObject
            {
                [fieldName] = new OpenApiArray
                {
                    new OpenApiString(message)
                }
            },
            ["correlationId"] = new OpenApiString("5b7f7b2d-5bb5-49d2-918d-c5d87ff54e3d")
        };
    }

    private static bool TryGetJsonResponse(OpenApiOperation operation, int statusCode, out OpenApiMediaType mediaType)
    {
        mediaType = null!;

        return operation.Responses.TryGetValue(statusCode.ToString(), out var response) &&
               response.Content.TryGetValue("application/json", out mediaType);
    }
}
