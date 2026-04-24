using System.Collections.Concurrent;

using Auth.Api.Options;

using Microsoft.Extensions.Options;

namespace Auth.Api.Middlewares;

public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitState> _states = new(StringComparer.Ordinal);

    public LoginRateLimitMiddleware(
        RequestDelegate next,
        IOptions<AuthOptions> authOptions,
        ILogger<LoginRateLimitMiddleware> logger)
    {
        _next = next;
        _authOptions = authOptions;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Path.Equals("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var options = _authOptions.Value.LoginRateLimit;
        var permitLimit = Math.Max(1, options.PermitLimit);
        var window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds));
        var now = DateTimeOffset.UtcNow;
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var state = _states.GetOrAdd(remoteAddress, _ => new RateLimitState());

        lock (state.Gate)
        {
            while (state.Attempts.Count > 0 && now - state.Attempts.Peek() >= window)
            {
                state.Attempts.Dequeue();
            }

            if (state.Attempts.Count >= permitLimit)
            {
                _logger.LogWarning("Rate limit excedido no login para remoteAddress={RemoteAddress}", remoteAddress);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            state.Attempts.Enqueue(now);
        }

        await _next(context);
    }

    private sealed class RateLimitState
    {
        public object Gate { get; } = new();

        public Queue<DateTimeOffset> Attempts { get; } = new();
    }
}
