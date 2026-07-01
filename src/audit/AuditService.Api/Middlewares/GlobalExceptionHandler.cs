using AuditService.Application.Common.Exceptions;
using AuditService.Domain.Exceptions;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;

namespace AuditService.Api.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> _logUnhandled =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(_logUnhandled)),
            "Erro nao tratado no AuditService. TraceId: {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (!IsHandledException(exception))
            _logUnhandled(logger, httpContext.TraceIdentifier, exception);

        await ToResult(exception).ExecuteAsync(httpContext);

        return true;
    }

    private static bool IsHandledException(Exception exception)
        => exception is ValidationException or ConflictException or DomainException;

    private static IResult ToResult(Exception exception)
        => exception switch
        {
            ValidationException validationException => Results.ValidationProblem(
                validationException.Errors
                    .GroupBy(static failure => NormalizeFieldName(failure.PropertyName))
                    .ToDictionary(
                        static group => group.Key,
                        static group => group.Select(failure => failure.ErrorMessage).ToArray()),
                title: "Invalid request",
                detail: "One or more validation errors occurred.",
                statusCode: StatusCodes.Status400BadRequest),
            ConflictException => Results.Problem(
                title: "Conflict",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict),
            DomainException => Results.Problem(
                title: "Violacao de regra de dominio",
                detail: exception.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Problem(
                title: "Erro interno",
                detail: "Ocorreu um erro inesperado.",
                statusCode: StatusCodes.Status500InternalServerError)
        };

    private static string NormalizeFieldName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "$"
            : string.Equals(value, nameof(Application.FunctionalAuditing.CreateAuditRecord.CreateAuditRecordCommand.IdempotencyKey), StringComparison.Ordinal)
            ? "Idempotency-Key"
            : char.ToLowerInvariant(value[0]) + value[1..];
    }
}
