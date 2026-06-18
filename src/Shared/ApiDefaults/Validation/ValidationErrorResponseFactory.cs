using ApiDefaults.Middlewares;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ApiDefaults.Validation;

public static class ValidationErrorResponseFactory
{
    public static TResponse Create<TResponse>(
        HttpContext httpContext,
        ValidationException exception,
        Func<Dictionary<string, string[]>, string?, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(createResponse);

        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(static failure => NormalizeFieldName(failure.PropertyName))
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Build(httpContext, errors, createResponse);
    }

    public static IActionResult CreateResult<TResponse>(
        ActionContext context,
        Func<Dictionary<string, string[]>, string?, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(createResponse);

        Dictionary<string, string[]> errors = context.ModelState
            .Where(static entry => entry.Value?.Errors.Count > 0)
            .GroupBy(static entry => NormalizeFieldName(entry.Key))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .SelectMany(static entry => entry.Value!.Errors.Select(GetErrorMessage))
                    .ToArray());

        TResponse response = Build(context.HttpContext, errors, createResponse);

        return new BadRequestObjectResult(response)
        {
            ContentTypes = { "application/json" }
        };
    }

    public static TResponse Create<TResponse>(
        HttpContext httpContext,
        string fieldName,
        string message,
        Func<Dictionary<string, string[]>, string?, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(createResponse);

        Dictionary<string, string[]> errors = new()
        {
            [NormalizeFieldName(fieldName)] = [message]
        };

        return Build(httpContext, errors, createResponse);
    }

    private static TResponse Build<TResponse>(
        HttpContext httpContext,
        Dictionary<string, string[]> errors,
        Func<Dictionary<string, string[]>, string?, TResponse> createResponse)
    {
        string? correlationId = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();
        return createResponse(errors, correlationId);
    }

    private static string GetErrorMessage(ModelError error)
        => string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? error.Exception?.Message ?? "The input was not valid."
            : error.ErrorMessage;

    private static string NormalizeFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "request", StringComparison.OrdinalIgnoreCase))
        {
            return "$";
        }

        string normalized = value.Trim();

        if (normalized.StartsWith("$.", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith("request.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["request.".Length..];
        }

        string[] path = normalized.Split('.');
        for (int i = 0; i < path.Length; i++)
        {
            path[i] = ToCamelCase(path[i]);
        }

        return string.Join('.', path);
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value[0] is '$' or '['
            ? value
            : value.Length == 1
            ? value.ToLowerInvariant()
            : string.Create(
                value.Length,
                value,
                static (buffer, source) =>
                {
                    buffer[0] = char.ToLowerInvariant(source[0]);
                    source.AsSpan(1).CopyTo(buffer[1..]);
                });
    }
}
