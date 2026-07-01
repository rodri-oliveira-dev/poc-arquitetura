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

        _logUnhandled(logger, httpContext.TraceIdentifier, exception);

        await Results.Problem(
            title: "Erro interno",
            detail: "Ocorreu um erro inesperado.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(httpContext);

        return true;
    }
}
