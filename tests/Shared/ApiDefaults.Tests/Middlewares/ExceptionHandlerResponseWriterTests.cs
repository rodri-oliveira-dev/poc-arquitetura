using System.Text.Json;

using ApiDefaults.Middlewares;
using ApiDefaults.Tests.Support;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;

namespace ApiDefaults.Tests.Middlewares;

public sealed class ExceptionHandlerResponseWriterTests
{
    [Fact]
    public async Task TryWriteValidationResponseAsync_WhenExceptionIsValidationException_ShouldWriteBadRequest()
    {
        DefaultHttpContext context = new HttpContextBuilder().WithPath("/commands").Build();
        var exception = new ValidationException([new ValidationFailure("amount", "Amount is required.")]);

        bool handled = await ExceptionHandlerResponseWriter.TryWriteValidationResponseAsync(
            context,
            exception,
            (_, validation) => new ValidationResponse(validation.Errors.Single().PropertyName, "correlation-1"),
            (_, field, message) => new ValidationResponse(field, message),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        JsonElement payload = await ReadJsonAsync(context);
        Assert.Equal("amount", payload.GetProperty("field").GetString());
        Assert.Equal("correlation-1", payload.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task TryWriteValidationResponseAsync_WhenExceptionWrapsJsonException_ShouldWriteJsonValidationResponse()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();
        var exception = new InvalidOperationException("Wrapper", new JsonException("Invalid JSON."));

        bool handled = await ExceptionHandlerResponseWriter.TryWriteValidationResponseAsync(
            context,
            exception,
            (_, _) => new ValidationResponse("unexpected", "unexpected"),
            (_, field, message) => new ValidationResponse(field, message),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        JsonElement payload = await ReadJsonAsync(context);
        Assert.Equal("$", payload.GetProperty("field").GetString());
        Assert.Equal("Request body must be valid JSON.", payload.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task TryWriteValidationResponseAsync_WhenExceptionIsNotValidationOrJson_ShouldReturnFalse()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();

        bool handled = await ExceptionHandlerResponseWriter.TryWriteValidationResponseAsync(
            context,
            new InvalidOperationException("Failure"),
            (_, _) => new ValidationResponse("unexpected", "unexpected"),
            (_, field, message) => new ValidationResponse(field, message),
            TestContext.Current.CancellationToken);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_WhenResponseHasNotStarted_ShouldWriteSafeProblemDetails()
    {
        DefaultHttpContext context = new HttpContextBuilder().WithPath("/failure").Build();

        await ExceptionHandlerResponseWriter.WriteProblemDetailsAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "Internal server error",
            "An unexpected error occurred.",
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        JsonElement payload = await ReadJsonAsync(context);
        Assert.Equal("Internal server error", payload.GetProperty("title").GetString());
        Assert.Equal("An unexpected error occurred.", payload.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, payload.GetProperty("status").GetInt32());
        Assert.Equal("https://httpstatuses.com/500", payload.GetProperty("type").GetString());
        Assert.Equal("/failure", payload.GetProperty("instance").GetString());
        Assert.Equal("trace-test", payload.GetProperty("traceId").GetString());
        Assert.DoesNotContain("stack", payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_WhenResponseAlreadyStarted_ShouldWriteProblemDetails()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();
        await context.Response.StartAsync(TestContext.Current.CancellationToken);

        await ExceptionHandlerResponseWriter.WriteProblemDetailsAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "Failure",
            "Failure detail.",
            TestContext.Current.CancellationToken);

        JsonElement payload = await ReadJsonAsync(context);
        Assert.Equal("Failure", payload.GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryWriteValidationResponseAsync_WhenSerializationFails_ShouldPropagateFailure()
    {
        DefaultHttpContext context = new HttpContextBuilder().Build();
        var exception = new ValidationException([new ValidationFailure("field", "message")]);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            ExceptionHandlerResponseWriter.TryWriteValidationResponseAsync<SerializationFailureResponse>(
                context,
                exception,
                (_, _) => new SerializationFailureResponse(),
                (_, _, _) => new SerializationFailureResponse(),
                TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_WhenCancellationIsRequested_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        DefaultHttpContext context = new HttpContextBuilder().Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExceptionHandlerResponseWriter.WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Failure",
                "Failure detail.",
                cancellation.Token));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }

    private sealed record ValidationResponse(string Field, string? CorrelationId);

    private sealed class SerializationFailureResponse
    {
        public Func<int> Value { get; } = () => 1;
    }
}
