using System.Text.Json;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;

namespace ApiDefaults.Middlewares;

public static class ExceptionHandlerResponseWriter
{
    public static async ValueTask<bool> TryWriteValidationResponseAsync<TValidationResponse>(
        HttpContext httpContext,
        Exception exception,
        Func<HttpContext, ValidationException, TValidationResponse> createValidationResponse,
        Func<HttpContext, string, string, TValidationResponse> createJsonValidationResponse,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(createValidationResponse);
        ArgumentNullException.ThrowIfNull(createJsonValidationResponse);

        if (exception is ValidationException validationException)
        {
            await WriteBadRequestAsync(
                httpContext,
                createValidationResponse(httpContext, validationException),
                cancellationToken);
            return true;
        }

        if (IsJsonRequestException(exception))
        {
            await WriteBadRequestAsync(
                httpContext,
                createJsonValidationResponse(httpContext, "$", "Request body must be valid JSON."),
                cancellationToken);
            return true;
        }

        return false;
    }

    public static Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(detail);

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private static Task WriteBadRequestAsync<TValidationResponse>(
        HttpContext httpContext,
        TValidationResponse validationResponse,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return httpContext.Response.WriteAsJsonAsync(validationResponse, cancellationToken);
    }

    private static bool IsJsonRequestException(Exception exception)
        => exception is JsonException or BadHttpRequestException ||
            (exception.InnerException is not null && IsJsonRequestException(exception.InnerException));
}
