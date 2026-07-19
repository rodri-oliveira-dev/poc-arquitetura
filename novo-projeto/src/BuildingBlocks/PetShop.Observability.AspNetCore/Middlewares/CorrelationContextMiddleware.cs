using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using PetShop.Observability.Context;
using PetShop.Observability.Propagation;

namespace PetShop.Observability.AspNetCore.Middlewares;

public sealed class CorrelationContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationContextMiddleware> _logger;

    public CorrelationContextMiddleware(
        RequestDelegate next,
        ILogger<CorrelationContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IExecutionContextAccessor executionContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionContextAccessor);

        string correlationId = ResolveOrCreateCorrelationId(context.Request.Headers);
        string? tenantId = NormalizeOptional(
            context.User.FindFirst(PropagationHeaderNames.TenantId)?.Value);

        Activity? activity = Activity.Current;
        activity?.SetTag(PropagationHeaderNames.CorrelationId, correlationId);
        activity?.SetBaggage(PropagationHeaderNames.CorrelationId, correlationId);

        if (tenantId is not null)
        {
            activity?.SetTag(PropagationHeaderNames.TenantId, tenantId);
            activity?.SetBaggage(PropagationHeaderNames.TenantId, tenantId);
        }

        PropagationContextSnapshot snapshot = PropagationContextSnapshot.CaptureCurrent(
            correlationId,
            tenantId);

        context.Request.Headers[PropagationHeaderNames.HttpCorrelationId] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[PropagationHeaderNames.HttpCorrelationId] = correlationId;
            return Task.CompletedTask;
        });

        using IDisposable executionScope = executionContextAccessor.Push(snapshot);
        using IDisposable? logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TenantId"] = tenantId,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["SpanId"] = activity?.SpanId.ToString()
        });

        await _next(context);
    }

    private static string ResolveOrCreateCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(
            PropagationHeaderNames.HttpCorrelationId,
            out StringValues values))
        {
            string? value = values.FirstOrDefault();
            if (Guid.TryParse(value, out Guid parsed))
            {
                return parsed.ToString();
            }
        }

        return Guid.NewGuid().ToString();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
