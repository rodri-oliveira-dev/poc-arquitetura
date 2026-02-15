using Microsoft.Extensions.Primitives;
using System.Diagnostics;

namespace BalanceService.Api.Middlewares;

/// <summary>
/// Garante que toda requisição tenha um CorrelationId válido (GUID) e o propaga
/// no request/response. Também cria um logging scope para enriquecer logs.
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
        var correlationId = ResolveOrCreateCorrelationId(context);

        // Observabilidade: se houver Activity (tracing distribuído / System.Diagnostics),
        // capturamos TraceId/SpanId para correlação em logs.
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString();
        var spanId = activity?.SpanId.ToString();

        // Propaga no request para consumo interno.
        context.Request.Headers[HeaderName] = correlationId;

        // Propaga no response.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            [ScopeKey] = correlationId,
            ["TraceId"] = traceId,
            ["SpanId"] = spanId
        }))
        {
            await _next(context);
        }
    }

    private static string ResolveOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues values))
        {
            var raw = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
                return parsed.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
