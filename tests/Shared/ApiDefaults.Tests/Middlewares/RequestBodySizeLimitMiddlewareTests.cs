using System.Text.Json;

using ApiDefaults.Middlewares;
using ApiDefaults.Tests.Support;

using Microsoft.AspNetCore.Http;

namespace ApiDefaults.Tests.Middlewares;

public sealed class RequestBodySizeLimitMiddlewareTests
{
    [Theory]
    [InlineData("POST", 9L)]
    [InlineData("POST", 10L)]
    public async Task InvokeAsync_WhenContentLengthIsWithinLimitOrMethodHasNoBody_ShouldCallNext(string method, long contentLength)
    {
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(method)
            .WithPath("/resource")
            .WithContentLength(contentLength)
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 10);

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenGetRequestHasContentLengthAboveLimit_ShouldRejectBeforeNext()
    {
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Get)
            .WithContentLength(100)
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 10);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, next.CallCount);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenContentLengthIsMissing_ShouldCallNext()
    {
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Post)
            .WithBody(new MemoryStream([1, 2, 3]), contentLength: null)
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 2);

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenContentLengthHeaderIsInvalid_ShouldCallNext()
    {
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Post)
            .WithHeader("Content-Length", "not-a-number")
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 2);

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenStreamingBodyHasNoContentLength_ShouldCallNext()
    {
        using var body = new NonSeekableReadOnlyStream([1, 2, 3, 4]);
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Post)
            .WithBody(body, contentLength: null)
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 2);

        await middleware.InvokeAsync(context);

        Assert.Equal(1, next.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenContentLengthExceedsLimit_ShouldWriteProblemDetailsAndSkipNext()
    {
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Post)
            .WithPath("/uploads")
            .WithContentLength(11)
            .Build();
        MiddlewareTestDelegate next = MiddlewareTestDelegate.Success();
        var middleware = CreateMiddleware(next, maxBytes: 10);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, next.CallCount);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        JsonElement payload = await ReadJsonAsync(context);
        Assert.Equal("Request body too large", payload.GetProperty("title").GetString());
        Assert.Equal("Request body must be at most 10 bytes.", payload.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, payload.GetProperty("status").GetInt32());
        Assert.Equal("https://httpstatuses.com/413", payload.GetProperty("type").GetString());
        Assert.Equal("/uploads", payload.GetProperty("instance").GetString());
        Assert.Equal("trace-test", payload.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsCanceledAndBodyIsRejected_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        DefaultHttpContext context = new HttpContextBuilder()
            .WithMethod(HttpMethods.Post)
            .WithContentLength(11)
            .WithCancellationToken(cancellation.Token)
            .Build();
        var middleware = CreateMiddleware(MiddlewareTestDelegate.Success(), maxBytes: 10);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    private static RequestBodySizeLimitMiddleware CreateMiddleware(MiddlewareTestDelegate next, long maxBytes)
        => new(next.ToRequestDelegate(), ServiceProviderTestFactory.ApiDefaultsOptions(maxBytes));

    private static async Task<JsonElement> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }

    private sealed class NonSeekableReadOnlyStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
    }
}
