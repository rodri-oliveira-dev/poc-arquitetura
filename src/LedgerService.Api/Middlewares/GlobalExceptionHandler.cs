using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LedgerService.Api.Contracts;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Domain.Exceptions;
using LedgerService.Api.Middlewares;

namespace LedgerService.Api.Middlewares;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Erro não tratado. TraceId: {TraceId}", httpContext.TraceIdentifier);

        var (statusCode, title, detail) = MapException(exception);

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => ToCamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var correlationId = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            var validationResponse = new ValidationErrorResponse
            {
                Errors = errors,
                CorrelationId = correlationId
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationResponse, cancellationToken);
            return true;
        }

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
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int statusCode, string title, string detail) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Invalid request", "One or more validation errors occurred."),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Violação de regra de domínio", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
