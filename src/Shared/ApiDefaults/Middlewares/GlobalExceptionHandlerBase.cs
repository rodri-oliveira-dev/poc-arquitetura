using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;

namespace ApiDefaults.Middlewares;

public abstract class GlobalExceptionHandlerBase<THandler, TValidationResponse>(
    ILogger<THandler> logger,
    Func<HttpContext, ValidationException, TValidationResponse> createValidationResponse,
    Func<HttpContext, string, string, TValidationResponse> createJsonValidationResponse) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> _logHandled =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(_logHandled)),
            "Excecao tratada. TraceId: {TraceId}");

    private static readonly Action<ILogger, string, Exception?> _logUnhandled =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, nameof(_logUnhandled)),
            "Erro nao tratado. TraceId: {TraceId}");

    private readonly ILogger<THandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        LogException(exception, httpContext.TraceIdentifier);

        var (statusCode, title, detail) = MapException(exception);

        if (await ExceptionHandlerResponseWriter.TryWriteValidationResponseAsync(
            httpContext,
            exception,
            createValidationResponse,
            createJsonValidationResponse,
            cancellationToken))
        {
            return true;
        }

        await ExceptionHandlerResponseWriter.WriteProblemDetailsAsync(
            httpContext,
            statusCode,
            title,
            detail,
            cancellationToken);

        return true;
    }

    protected abstract bool IsHandledException(Exception exception);

    protected abstract (int statusCode, string title, string detail) MapException(Exception exception);

    private void LogException(Exception exception, string traceId)
    {
        if (IsHandledException(exception))
        {
            _logHandled(_logger, traceId, exception);
            return;
        }

        _logUnhandled(_logger, traceId, exception);
    }
}
