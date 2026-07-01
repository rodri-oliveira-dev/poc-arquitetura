using System.Text.Json;

using AuditService.Api.Middlewares;
using AuditService.Application.Common.Exceptions;
using AuditService.Domain.Exceptions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuditService.Api.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_should_return_validation_problem_for_validation_exception()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("IdempotencyKey", "Idempotency-Key is required."),
            new ValidationFailure("SourceService", "SourceService is required."),
            new ValidationFailure("", "Payload is invalid.")
        ]);

        (HttpContext context, JsonDocument body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Invalid request", body.RootElement.GetProperty("title").GetString());
        JsonElement errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Idempotency-Key", out _));
        Assert.True(errors.TryGetProperty("sourceService", out _));
        Assert.True(errors.TryGetProperty("$", out _));
    }

    [Fact]
    public async Task TryHandleAsync_should_return_conflict_problem_for_conflict_exception()
    {
        (HttpContext context, JsonDocument body) = await HandleAsync(new ConflictException("Audit record already exists."));

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("Conflict", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Audit record already exists.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_should_return_unprocessable_entity_for_domain_exception()
    {
        (HttpContext context, JsonDocument body) = await HandleAsync(new DomainException("Status invalido."));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.Equal("Violacao de regra de dominio", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Status invalido.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_should_return_internal_error_for_unexpected_exception()
    {
        (HttpContext context, JsonDocument body) = await HandleAsync(new InvalidOperationException("database unavailable"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("Erro interno", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Ocorreu um erro inesperado.", body.RootElement.GetProperty("detail").GetString());
    }

    private static async Task<(HttpContext Context, JsonDocument Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-1",
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (context, body);
    }
}
