using BalanceService.Api.Middlewares;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BalanceService.Api.Contracts;

public static class ValidationErrorResponseFactory
{
    public static ValidationErrorResponse Create(HttpContext httpContext, ValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var errors = exception.Errors
            .GroupBy(e => NormalizeFieldName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Create(httpContext, errors);
    }

    public static IActionResult CreateResult(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .GroupBy(entry => NormalizeFieldName(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors.Select(GetErrorMessage))
                    .ToArray());

        var response = Create(context.HttpContext, errors);

        return new BadRequestObjectResult(response)
        {
            ContentTypes = { "application/json" }
        };
    }

    public static ValidationErrorResponse Create(HttpContext httpContext, string fieldName, string message)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return Create(httpContext, new Dictionary<string, string[]>
        {
            [NormalizeFieldName(fieldName)] = new[] { message }
        });
    }

    private static ValidationErrorResponse Create(HttpContext httpContext, Dictionary<string, string[]> errors)
    {
        var correlationId = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

        return new ValidationErrorResponse
        {
            Errors = errors,
            CorrelationId = correlationId
        };
    }

    private static string GetErrorMessage(ModelError error)
        => !string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? error.ErrorMessage
            : error.Exception?.Message ?? "The input was not valid.";

    private static string NormalizeFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "request", StringComparison.OrdinalIgnoreCase))
            return "$";

        var normalized = value.Trim();
        if (normalized.StartsWith("$.", StringComparison.Ordinal))
            normalized = normalized[2..];

        if (normalized.StartsWith("request.", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["request.".Length..];

        return string.Join('.', normalized.Split('.').Select(ToCamelCase));
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] is '$' or '[')
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
