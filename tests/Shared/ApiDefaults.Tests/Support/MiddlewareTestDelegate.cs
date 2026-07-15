using Microsoft.AspNetCore.Http;

namespace ApiDefaults.Tests.Support;

internal sealed class MiddlewareTestDelegate
{
    private readonly Func<HttpContext, Task> _handler;

    private MiddlewareTestDelegate(Func<HttpContext, Task> handler)
    {
        _handler = handler;
    }

    public int CallCount
    {
        get; private set;
    }

    public HttpContext? LastContext
    {
        get; private set;
    }

    public RequestDelegate ToRequestDelegate()
        => async context =>
        {
            CallCount++;
            LastContext = context;
            await _handler(context);
        };

    public static MiddlewareTestDelegate Success()
        => new(_ => Task.CompletedTask);

    public static MiddlewareTestDelegate Throw(Exception exception)
        => new(_ => Task.FromException(exception));

    public static MiddlewareTestDelegate WriteResponse(string body, int statusCode = StatusCodes.Status200OK)
        => new(async context =>
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(body, context.RequestAborted);
        });

    public static MiddlewareTestDelegate SetStatusCode(int statusCode)
        => new(context =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });

    public static MiddlewareTestDelegate ObserveCancellation()
        => new(context => Task.FromCanceled(context.RequestAborted));
}
