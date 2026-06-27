using System.Net;
using System.Text.Json;

using IdentityService.Api.Middlewares;
using IdentityService.Application.Common.Exceptions;
using IdentityService.Domain.Exceptions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IdentityService.UnitTests.Api.Middlewares;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_should_return_422_for_domain_exception_Async()
    {
        var result = await HandleAsync(new DomainException("MerchantId is required."));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal("Violacao de regra de dominio", result.Title);
        Assert.Equal("MerchantId is required.", result.Detail);
        Assert.Equal("/api/v1/users", result.Instance);
        Assert.Equal("trace-identity", result.TraceId);
    }

    [Fact]
    public async Task TryHandleAsync_should_return_409_for_identity_provider_conflict_Async()
    {
        var result = await HandleAsync(new IdentityProviderException(
            IdentityProviderErrorKind.Conflict,
            "Keycloak rejeitou a criacao do usuario porque email ou username ja existem.",
            HttpStatusCode.Conflict));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Conflict", result.Title);
        Assert.Contains("email ou username", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_should_sanitize_identity_provider_unauthorized_error_Async()
    {
        const string providerSecret = "admin-secret-from-provider";

        var result = await HandleAsync(new IdentityProviderException(
            IdentityProviderErrorKind.Unauthorized,
            $"provider rejected client secret {providerSecret}",
            HttpStatusCode.Unauthorized));

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("Identity provider unauthorized", result.Title);
        Assert.Equal(
            "O provider de identidade recusou as credenciais administrativas configuradas.",
            result.Detail);
        Assert.DoesNotContain(providerSecret, result.RawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_should_return_problem_details_without_stack_trace_for_unexpected_error_Async()
    {
        var result = await HandleAsync(new InvalidOperationException("database password leaked"));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Erro interno", result.Title);
        Assert.Equal("Ocorreu um erro inesperado.", result.Detail);
        Assert.DoesNotContain("database password leaked", result.RawBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), result.RawBody, StringComparison.Ordinal);
    }

    private static async Task<ProblemResult> HandleAsync(Exception exception)
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-identity"
        };
        httpContext.Request.Path = "/api/v1/users";
        httpContext.Response.Body = new MemoryStream();

        var logger = new CapturingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);

        var handled = await handler.TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken);

        Assert.True(handled);

        httpContext.Response.Body.Position = 0;
        using StreamReader reader = new(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new ProblemResult(
            httpContext.Response.StatusCode,
            root.GetProperty("title").GetString() ?? string.Empty,
            root.GetProperty("detail").GetString() ?? string.Empty,
            root.GetProperty("instance").GetString() ?? string.Empty,
            root.GetProperty("traceId").GetString() ?? string.Empty,
            body,
            logger.Messages);
    }

    private sealed record ProblemResult(
        int StatusCode,
        string Title,
        string Detail,
        string Instance,
        string TraceId,
        string RawBody,
        IReadOnlyCollection<string> LogMessages);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages
        {
            get;
        } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
