using System.Diagnostics;

using Microsoft.Extensions.Primitives;

namespace ApiDefaults.Middlewares;

/// <summary>
/// Garante que toda requisicao tenha um CorrelationId valido (GUID) e o propaga
/// no request/response. Tambem cria um logging scope para enriquecer logs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ScopeKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = ResolveOrCreateCorrelationId(context);
        Activity? activity = Activity.Current;
        string? traceId = activity?.TraceId.ToString();
        string? spanId = activity?.SpanId.ToString();

        context.Request.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(
            "{CorrelationIdKey}={CorrelationId} TraceId={TraceId} SpanId={SpanId}",
            ScopeKey,
            correlationId,
            traceId,
            spanId))
        {
            await _next(context);
        }
    }

    private static string ResolveOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues values))
        {
            string? raw = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out Guid parsed))
            {
                return parsed.ToString();
            }
        }

        return Guid.NewGuid().ToString();
    }
}
