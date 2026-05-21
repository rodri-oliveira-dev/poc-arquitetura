using System.Diagnostics;

using Auth.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Auth.UnitTests.Middlewares;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_should_use_readable_structured_logging_scope_when_correlation_id_is_explicit()
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        using Activity activity = new("test-request");
        activity.Start();
        var traceId = activity.TraceId.ToString();
        var spanId = activity.SpanId.ToString();

        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            logger);

        await middleware.InvokeAsync(context);
        context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(correlationId);

        var scope = logger.Scopes.Should().ContainSingle().Subject;
        scope.ToString().Should().Contain($"CorrelationId={correlationId}");
        scope.ToString().Should().Contain($"TraceId={traceId}");
        scope.ToString().Should().Contain($"SpanId={spanId}");

        var values = ScopeValues(scope);
        values["CorrelationId"].Should().Be(correlationId);
        values["TraceId"].Should().Be(traceId);
        values["SpanId"].Should().Be(spanId);
    }

    [Fact]
    public async Task InvokeAsync_should_generate_correlation_id_when_header_is_missing()
    {
        var context = new DefaultHttpContext();
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            logger);

        await middleware.InvokeAsync(context);
        var generated = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Guid.TryParse(generated, out _).Should().BeTrue();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(generated);

        var scope = logger.Scopes.Should().ContainSingle().Subject;
        scope.ToString().Should().Contain($"CorrelationId={generated}");

        var values = ScopeValues(scope);
        values["CorrelationId"].Should().Be(generated);
    }

    private static Dictionary<string, object?> ScopeValues(object scope)
        => scope
            .Should()
            .BeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>()
            .Subject
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<object> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            Scopes.Add(state);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
