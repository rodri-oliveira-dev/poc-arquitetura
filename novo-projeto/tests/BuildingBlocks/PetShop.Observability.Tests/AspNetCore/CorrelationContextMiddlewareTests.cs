using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using PetShop.Observability.AspNetCore.Middlewares;
using PetShop.Observability.Context;
using PetShop.Observability.Propagation;

using Xunit;

namespace PetShop.Observability.Tests.AspNetCore;

public sealed class CorrelationContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldExposeCorrelationAndTenantInsideRequestScope()
    {
        string correlationId = Guid.NewGuid().ToString();
        var accessor = new ExecutionContextAccessor();
        PropagationContextSnapshot? captured = null;
        RequestDelegate next = _ =>
        {
            captured = accessor.Current;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationContextMiddleware(
            next,
            NullLogger<CorrelationContextMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers[PropagationHeaderNames.HttpCorrelationId] = correlationId;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PropagationHeaderNames.TenantId, "tenant-a")],
            "test"));

        await middleware.InvokeAsync(context, accessor);

        Assert.NotNull(captured);
        Assert.Equal(correlationId, captured.Value.CorrelationId);
        Assert.Equal("tenant-a", captured.Value.TenantId);
        Assert.Equal(correlationId, context.Request.Headers[PropagationHeaderNames.HttpCorrelationId]);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReplaceInvalidCorrelationIdWithGuid()
    {
        var accessor = new ExecutionContextAccessor();
        PropagationContextSnapshot? captured = null;
        RequestDelegate next = _ =>
        {
            captured = accessor.Current;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationContextMiddleware(
            next,
            NullLogger<CorrelationContextMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers[PropagationHeaderNames.HttpCorrelationId] = "invalid";

        await middleware.InvokeAsync(context, accessor);

        Assert.NotNull(captured);
        Assert.True(Guid.TryParse(captured.Value.CorrelationId, out _));
        Assert.Null(captured.Value.TenantId);
    }
}
