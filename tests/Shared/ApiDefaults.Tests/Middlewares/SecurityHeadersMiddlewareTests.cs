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
        context.Response.Headers.XFrameOptions = "SAMEORIGIN";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
        var middleware = new SecurityHeadersMiddleware(MiddlewareTestDelegate.Success().ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal("SAMEORIGIN", context.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("default-src 'none'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Single(context.Response.Headers.XFrameOptions);
        Assert.Single(context.Response.Headers.ContentSecurityPolicy);
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
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
    }

    private static void AssertSecurityHeaders(HttpContext context, bool expectHsts)
    {
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("none", context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString());
        Assert.Equal("geolocation=(), microphone=(), camera=()", context.Response.Headers["Permissions-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"].ToString());
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, context.Response.Headers.ContentSecurityPolicy.ToString());

        if (expectHsts)
        {
            Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers.StrictTransportSecurity.ToString());
        }
        else
        {
            Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        }

        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/swagger-ui.css")]
    public async Task InvokeAsync_WhenRequestTargetsSwaggerUi_ShouldUseSwaggerCsp(string path)
    {
        DefaultHttpContext context = new HttpContextBuilder().WithHttps().Build();
        context.Request.Path = path;
        var middleware = new SecurityHeadersMiddleware(MiddlewareTestDelegate.Success().ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal(SecurityHeadersMiddleware.SwaggerUiContentSecurityPolicy, context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestTargetsOpenApiJson_ShouldUseApiCsp()
    {
        DefaultHttpContext context = new HttpContextBuilder().WithHttps().Build();
        context.Request.Path = "/swagger/v1/swagger.json";
        var middleware = new SecurityHeadersMiddleware(MiddlewareTestDelegate.Success().ToRequestDelegate());

        await middleware.InvokeAsync(context);

        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, context.Response.Headers.ContentSecurityPolicy.ToString());
    }
}
