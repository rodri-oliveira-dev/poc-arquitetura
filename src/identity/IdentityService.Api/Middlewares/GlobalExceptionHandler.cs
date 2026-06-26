using IdentityService.Application.Common.Exceptions;
using IdentityService.Domain.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace IdentityService.Api.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> _logUnhandled =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(_logUnhandled)),
            "Erro nao tratado no IdentityService. TraceId: {TraceId}");

    private static readonly Action<ILogger, string, Exception?> _logHandled =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(_logHandled)),
            "Excecao tratada no IdentityService. TraceId: {TraceId}");

    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, title, detail) = MapException(exception);
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logUnhandled(_logger, httpContext.TraceIdentifier, exception);
        }
        else
        {
            _logHandled(_logger, httpContext.TraceIdentifier, exception);
        }

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int statusCode, string title, string detail) MapException(Exception exception)
        => exception switch
        {
            DomainException => (
                StatusCodes.Status422UnprocessableEntity,
                "Violacao de regra de dominio",
                exception.Message),
            IdentityProviderException { Kind: IdentityProviderErrorKind.Conflict } => (
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            IdentityProviderException { Kind: IdentityProviderErrorKind.Unauthorized } => (
                StatusCodes.Status502BadGateway,
                "Identity provider unauthorized",
                "O provider de identidade recusou as credenciais administrativas configuradas."),
            IdentityProviderException => (
                StatusCodes.Status502BadGateway,
                "Identity provider error",
                exception.Message),
            _ when IsUniqueViolation(exception) => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "Usuario ja cadastrado."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado.")
        };

    private static bool IsUniqueViolation(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException postgresException }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
