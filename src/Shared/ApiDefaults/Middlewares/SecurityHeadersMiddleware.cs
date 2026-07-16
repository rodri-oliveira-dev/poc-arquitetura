namespace ApiDefaults.Middlewares;

public sealed class SecurityHeadersMiddleware
{
    public const string ApiContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
    public const string SwaggerUiContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Response.HasStarted)
        {
            context.Response.OnStarting(static state =>
            {
                var httpContext = (HttpContext)state;
                AddHeaders(httpContext);
                return Task.CompletedTask;
            }, context);
        }

        AddHeaders(context);

        await _next(context);
    }

    private static void AddHeaders(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
        headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
        headers.TryAdd("Content-Security-Policy", ResolveContentSecurityPolicy(context.Request.Path));

        if (context.Request.IsHttps)
        {
            headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }
    }

    private static string ResolveContentSecurityPolicy(PathString path)
    {
        if (IsSwaggerUiPath(path))
        {
            return SwaggerUiContentSecurityPolicy;
        }

        return ApiContentSecurityPolicy;
    }

    private static bool IsSwaggerUiPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        string value = path.Value;

        if (!value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !value.EndsWith("/swagger.json", StringComparison.OrdinalIgnoreCase);
    }
}
