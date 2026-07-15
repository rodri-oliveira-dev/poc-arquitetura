using System.Text.Json;

using ApiDefaults.Middlewares;
using ApiDefaults.Tests.Support;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiDefaults.Tests.Middlewares;

public sealed class GlobalExceptionHandlerBaseTests
{
    public static TheoryData<Exception, int, string, string, LogLevel> ExceptionMappings => new()
    {
        { new ValidationException([new ValidationFailure("field", "message")]), StatusCodes.Status400BadRequest, "validation", "validation", LogLevel.Warning },
        { new DomainRuleException("Domain failed."), StatusCodes.Status422UnprocessableEntity, "Domain rule violated", "Domain failed.", LogLevel.Warning },
        { new ResourceNotFoundException("Missing."), StatusCodes.Status404NotFound, "Resource not found", "Missing.", LogLevel.Warning },
        { new ResourceConflictException("Conflict."), StatusCodes.Status409Conflict, "Resource conflict", "Conflict.", LogLevel.Warning },
        { new UnauthorizedAccessException("Unauthorized."), StatusCodes.Status401Unauthorized, "Unauthorized", "Unauthorized.", LogLevel.Warning },
        { new ForbiddenAccessException("Forbidden."), StatusCodes.Status403Forbidden, "Forbidden", "Forbidden.", LogLevel.Warning },
        { new OperationCanceledException("Canceled."), StatusCodes.Status499ClientClosedRequest, "Client closed request", "Canceled.", LogLevel.Warning },
        { new TimeoutException("Timed out."), StatusCodes.Status504GatewayTimeout, "Timeout", "Timed out.", LogLevel.Warning },
        { new InvalidOperationException("Sensitive failure."), StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.", LogLevel.Error }
    };

    [Theory]
    [MemberData(nameof(ExceptionMappings))]
    public async Task TryHandleAsync_WhenExceptionIsMapped_ShouldWriteExpectedResponseAndLog(
        Exception exception,
        int expectedStatus,
        string expectedTitle,
        string expectedDetail,
        LogLevel expectedLevel)
    {
        DefaultHttpContext context = new HttpContextBuilder().WithPath("/operation").Build();
        var logger = new TestLogger<TestGlobalExceptionHandler>();
        var handler = new TestGlobalExceptionHandler(logger);

        bool handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        TestLogger<TestGlobalExceptionHandler>.LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal(exception, entry.Exception);
        JsonElement payload = await ReadJsonAsync(context);

        if (exception is ValidationException)
        {
            Assert.Equal("field", payload.GetProperty("field").GetString());
            Assert.Equal("message", payload.GetProperty("message").GetString());
            return;
        }

        Assert.Equal(expectedTitle, payload.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, payload.GetProperty("detail").GetString());
        Assert.Equal(expectedStatus, payload.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{expectedStatus}", payload.GetProperty("type").GetString());
        Assert.Equal("trace-test", payload.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Sensitive failure", payload.GetRawText(), StringComparison.Ordinal);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }

    private sealed class TestGlobalExceptionHandler(ILogger<TestGlobalExceptionHandler> logger)
        : GlobalExceptionHandlerBase<TestGlobalExceptionHandler, ValidationResponse>(
            logger,
            (_, exception) =>
            {
                ValidationFailure failure = exception.Errors.Single();
                return new ValidationResponse(failure.PropertyName, failure.ErrorMessage);
            },
            (_, field, message) => new ValidationResponse(field, message))
    {
        protected override bool IsHandledException(Exception exception)
            => exception is ValidationException
                or DomainRuleException
                or ResourceNotFoundException
                or ResourceConflictException
                or UnauthorizedAccessException
                or ForbiddenAccessException
                or OperationCanceledException
                or TimeoutException;

        protected override (int statusCode, string title, string detail) MapException(Exception exception)
            => exception switch
            {
                DomainRuleException domain => (StatusCodes.Status422UnprocessableEntity, "Domain rule violated", domain.Message),
                ResourceNotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Message),
                ResourceConflictException conflict => (StatusCodes.Status409Conflict, "Resource conflict", conflict.Message),
                UnauthorizedAccessException unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized", unauthorized.Message),
                ForbiddenAccessException forbidden => (StatusCodes.Status403Forbidden, "Forbidden", forbidden.Message),
                OperationCanceledException canceled => (StatusCodes.Status499ClientClosedRequest, "Client closed request", canceled.Message),
                TimeoutException timeout => (StatusCodes.Status504GatewayTimeout, "Timeout", timeout.Message),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.")
            };
    }

    private sealed record ValidationResponse(string Field, string Message);

    private sealed class DomainRuleException(string message) : Exception(message);

    private sealed class ResourceNotFoundException(string message) : Exception(message);

    private sealed class ResourceConflictException(string message) : Exception(message);

    private sealed class ForbiddenAccessException(string message) : Exception(message);
}
