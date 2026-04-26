using LedgerService.Api.Options;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LedgerService.Api.Middlewares;

public sealed class RequestBodySizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<ApiLimitsOptions> _options;

    public RequestBodySizeLimitMiddleware(RequestDelegate next, IOptions<ApiLimitsOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var maxRequestBodySizeBytes = _options.Value.MaxRequestBodySizeBytes;

        if (maxRequestBodySizeBytes > 0 &&
            context.Request.ContentLength is long contentLength &&
            contentLength > maxRequestBodySizeBytes)
        {
            var problemDetails = new ProblemDetails
            {
                Title = "Request body too large",
                Detail = $"Request body must be at most {maxRequestBodySizeBytes} bytes.",
                Status = StatusCodes.Status413PayloadTooLarge,
                Type = "https://httpstatuses.com/413",
                Instance = context.Request.Path
            };

            problemDetails.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(problemDetails, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
