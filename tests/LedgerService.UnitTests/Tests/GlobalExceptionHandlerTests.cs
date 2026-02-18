using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace LedgerService.UnitTests.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_should_return_400_and_validation_error_response_for_validationexception()
    {
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/x";
        ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = Guid.NewGuid().ToString();
        ctx.Response.Body = new MemoryStream();

        var ex = new ValidationException(new[]
        {
            new ValidationFailure("MerchantId", "required"),
            new ValidationFailure("MerchantId", "another")
        });

        var handled = await sut.TryHandleAsync(ctx, ex, CancellationToken.None);

        handled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("errors").GetProperty("merchantId").GetArrayLength().Should().Be(2);
        doc.RootElement.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory(Skip ="Ajustar")]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict)]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(DomainException), StatusCodes.Status422UnprocessableEntity)]
    public async Task TryHandleAsync_should_map_known_exceptions(Type exType, int expectedStatus)
    {
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/x";
        ctx.Response.Body = new MemoryStream();

        var ex = (Exception)Activator.CreateInstance(exType, "boom")!;

        var handled = await sut.TryHandleAsync(ctx, ex, CancellationToken.None);

        handled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(expectedStatus);

        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        doc.RootElement.TryGetProperty("extensions", out var ext).Should().BeTrue();
        ext.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }
}
