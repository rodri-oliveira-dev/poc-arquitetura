using FluentValidation;
using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        LogHandledException(exception, httpContext.TraceIdentifier);

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

    private void LogHandledException(Exception exception, string traceId)
    {
        if (exception is ValidationException or ForbiddenException or ConflictException or NotFoundException or DomainException
            || IsEstornoActiveUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Excecao tratada. TraceId: {TraceId}", traceId);
            return;
        }

        _logger.LogError(exception, "Erro nao tratado. TraceId: {TraceId}", traceId);
    }

    private static (int statusCode, string title, string detail) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Invalid request", "One or more validation errors occurred."),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            _ when IsEstornoActiveUniqueViolation(exception) => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "Lancamento ja possui solicitacao ativa de estorno."),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso nao encontrado", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };
    }

    private static bool IsEstornoActiveUniqueViolation(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException postgresException }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                "ux_estornos_lancamentos_original_active",
                StringComparison.Ordinal);

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
