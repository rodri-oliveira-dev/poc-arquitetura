using System.Diagnostics;

using ApiDefaults.Middlewares;
using ApiDefaults.Tests.Support;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiDefaults.Tests.Middlewares;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_should_use_readable_structured_logging_scope_when_correlation_id_is_explicit()
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var logger = new TestLogger<CorrelationIdMiddleware>();
        using Activity activity = new("test-request");
        activity.Start();
        var traceId = activity.TraceId.ToString();
        var spanId = activity.SpanId.ToString();

        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            logger);

        await middleware.InvokeAsync(context);
        Assert.Equal(correlationId, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        var scope = Assert.Single(logger.Scopes);
        Assert.Contains($"CorrelationId={correlationId}", scope.ToString());
        Assert.Contains($"TraceId={traceId}", scope.ToString());
        Assert.Contains($"SpanId={spanId}", scope.ToString());
        var values = ScopeValues(scope);
        Assert.Equal(correlationId, values["CorrelationId"]);
        Assert.Equal(traceId, values["TraceId"]);
        Assert.Equal(spanId, values["SpanId"]);
    }

    [Fact]
    public async Task InvokeAsync_should_generate_correlation_id_when_header_is_missing()
    {
        var context = new DefaultHttpContext();
        var logger = new TestLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            logger);

        await middleware.InvokeAsync(context);
        var generated = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParse(generated, out _));
        Assert.Equal(generated, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        var scope = Assert.Single(logger.Scopes);
        Assert.Contains($"CorrelationId={generated}", scope.ToString());
        var values = ScopeValues(scope);
        Assert.Equal(generated, values["CorrelationId"]);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdIsValid_ShouldKeepItOnRequest()
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var middleware = new CorrelationIdMiddleware(
            MiddlewareTestDelegate.Success().ToRequestDelegate(),
            new TestLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000-extra")]
    public async Task InvokeAsync_WhenCorrelationIdIsEmptyOrInvalid_ShouldReplaceIt(string incoming)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(
            MiddlewareTestDelegate.Success().ToRequestDelegate(),
            new TestLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        string generated = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParse(generated, out _));
        Assert.NotEqual(incoming, generated);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderHasMultipleValues_ShouldUseFirstValidValue()
    {
        var first = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var second = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new StringValues([first, second]);
        var middleware = new CorrelationIdMiddleware(
            MiddlewareTestDelegate.Success().ToRequestDelegate(),
            new TestLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal(Guid.Parse(first).ToString(), context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ShouldKeepCorrelationIdOnRequest()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(
            MiddlewareTestDelegate.Throw(new InvalidOperationException("failure")).ToRequestDelegate(),
            new TestLogger<CorrelationIdMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.True(Guid.TryParse(context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), out _));
    }

    private static Dictionary<string, object?> ScopeValues(object scope)
        => Assert
            .IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(scope)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

}
