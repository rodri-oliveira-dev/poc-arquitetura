using ApiDefaults.Middlewares;
using ApiDefaults.Tests.Support;

using Microsoft.AspNetCore.Http;

namespace ApiDefaults.Tests.Middlewares;

public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_WhenPipelineCompletes_ShouldAddSecurityHeadersAndCallNext(int statusCode)
    {
        DefaultHttpContext context = new HttpContextBuilder().WithHttps().Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.SetStatusCode(statusCode);
        var middleware = new SecurityHeadersMiddleware(next.ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
        Assert.Equal(statusCode, context.Response.StatusCode);
        AssertSecurityHeaders(context, expectHsts: true);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeadersAlreadyExist_ShouldNotOverwriteThem()
    {
        DefaultHttpContext context = new HttpContextBuilder().WithHttps().Build();
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        var middleware = new SecurityHeadersMiddleware(MiddlewareTestDelegate.Success().ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("default-src 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Single(context.Response.Headers["X-Frame-Options"]);
        Assert.Single(context.Response.Headers["Content-Security-Policy"]);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsHttp_ShouldNotAddStrictTransportSecurity()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();
        var middleware = new SecurityHeadersMiddleware(MiddlewareTestDelegate.Success().ToRequestDelegate());

        await middleware.InvokeAsync(context);

        AssertSecurityHeaders(context, expectHsts: false);
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseAlreadyStarted_ShouldStillCallNext()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();
        await context.Response.StartAsync(TestContext.Current.CancellationToken);
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = new SecurityHeadersMiddleware(next.ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    private static void AssertSecurityHeaders(HttpContext context, bool expectHsts)
    {
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("none", context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString());
        Assert.Equal("geolocation=(), microphone=(), camera=()", context.Response.Headers["Permissions-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"].ToString());
        Assert.Equal(
            "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'",
            context.Response.Headers["Content-Security-Policy"].ToString());

        if (expectHsts)
        {
            Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers["Strict-Transport-Security"].ToString());
        }
        else
        {
            Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        }

        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }
}
