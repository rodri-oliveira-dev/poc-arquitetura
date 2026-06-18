using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using TransferService.Api.Middlewares;
using TransferService.Application.Common.Exceptions;
using TransferService.Domain.Exceptions;

namespace TransferService.IntegrationTests.Api.Middlewares;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(ForbiddenException), StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict, "Conflict")]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, "Recurso nao encontrado")]
    [InlineData(typeof(DomainException), StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, "Erro interno")]
    public async Task TryHandleAsync_should_map_exception_to_problem_details(Type exceptionType, int statusCode, string title)
    {
        var context = CreateContext();
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await sut.TryHandleAsync(context, CreateException(exceptionType), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(statusCode, context.Response.StatusCode);
        var json = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(title, document.RootElement.GetProperty("title").GetString());
        Assert.Equal(statusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(context.TraceIdentifier, document.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_should_return_validation_error_response()
    {
        var context = CreateContext();
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var exception = new ValidationException([new ValidationFailure("request.Amount", "Amount is required.")]);

        var handled = await sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var json = await ReadResponseAsync(context);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task TryHandleAsync_should_return_validation_error_for_json_exception()
    {
        var context = CreateContext();
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await sut.TryHandleAsync(context, new JsonException("invalid json"), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("Request body must be valid JSON.", await ReadResponseAsync(context), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_should_validate_arguments()
    {
        var context = CreateContext();
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryHandleAsync(null!, new InvalidOperationException(), CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TryHandleAsync(context, null!, CancellationToken.None).AsTask());
    }

    private static Exception CreateException(Type exceptionType)
        => exceptionType == typeof(ForbiddenException) ? new ForbiddenException("sem permissao")
        : exceptionType == typeof(ConflictException) ? new ConflictException("conflito")
        : exceptionType == typeof(NotFoundException) ? new NotFoundException("nao encontrado")
        : exceptionType == typeof(DomainException) ? new DomainException("regra invalida")
        : new InvalidOperationException("erro");

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = Guid.NewGuid().ToString()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
