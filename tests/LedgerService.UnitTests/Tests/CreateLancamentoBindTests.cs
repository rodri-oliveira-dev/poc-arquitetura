using FluentAssertions;
using FluentValidation;
using LedgerService.Api.Contracts;
using LedgerService.Api.Controllers.Binds;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.UnitTests.Tests;

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

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.IdempotencyKey));
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

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateLancamentoInput.IdempotencyKey));
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

        result.CorrelationId.Should().Be(middlewareCorrelationId);
        result.Type.Should().Be("CREDIT");
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
        double amount = 10.0)
        => new(
            MerchantId: merchantId,
            Type: type,
            Amount: amount,
            Description: null,
            ExternalReference: null);
}
