using FluentValidation;
using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Api.Controllers.Binds;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.UnitTests.Api.Binds;

public sealed class CreateLancamentoBindTests
{
    [Fact]
    public async Task BindAsync_should_fail_when_idempotency_key_is_missing()
    {
        var context = BuildHttpContext();

        var act = () => CreateLancamentoBind.BindAsync(
            context,
            idempotencyKey: "",
            correlationId: Guid.NewGuid().ToString(),
            request: ValidRequest(),
            cancellationToken: CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ValidationException>(act);
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateLancamentoInput.IdempotencyKey));
    }

    [Fact]
    public async Task BindAsync_should_fail_when_idempotency_key_is_not_guid()
    {
        var context = BuildHttpContext();

        var act = () => CreateLancamentoBind.BindAsync(
            context,
            idempotencyKey: "not-a-guid",
            correlationId: Guid.NewGuid().ToString(),
            request: ValidRequest(),
            cancellationToken: CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ValidationException>(act);
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateLancamentoInput.IdempotencyKey));
    }

    [Fact]
    public async Task BindAsync_should_use_middleware_correlation_id_when_header_param_is_null()
    {
        var context = BuildHttpContext();
        var middlewareCorrelationId = Guid.NewGuid().ToString();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = middlewareCorrelationId;

        var result = await CreateLancamentoBind.BindAsync(
            context,
            idempotencyKey: Guid.NewGuid().ToString(),
            correlationId: null,
            request: ValidRequest(type: "credit"),
            cancellationToken: CancellationToken.None);
        Assert.Equal(middlewareCorrelationId, result.CorrelationId);
        Assert.Equal("CREDIT", result.Type);
    }

    private static DefaultHttpContext BuildHttpContext()
    {
        var services = new ServiceCollection()
            .AddTransient<IValidator<CreateLancamentoInput>, CreateLancamentoInputValidator>()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services
        };
    }

    private static CreateLancamentoRequest ValidRequest(
        string merchantId = "m1",
        string type = "CREDIT",
        decimal amount = 10.0m)
        => new(
            MerchantId: merchantId,
            Type: type,
            Amount: amount,
            Description: null,
            ExternalReference: null);
}
