using FluentValidation;
using LedgerService.Api.Contracts;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace LedgerService.Api.Controllers.Binds;

public static class CreateLancamentoBind
{
    public static async Task<CreateLancamentoInput> BindAsync(
        HttpContext httpContext,
        string idempotencyKey,
        string? correlationId,
        CreateLancamentoRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        var normalizedType = (request.Type ?? string.Empty).Trim().ToUpperInvariant();


        var input = new CreateLancamentoInput(
            request.MerchantId,
            normalizedType,
            request.Amount.ToString(CultureInfo.InvariantCulture),
            request.Description,
            request.ExternalReference,
            idempotencyKey,
            resolvedCorrelationId);

        // Validação no Bind (fail-fast)
        // IMPORTANT: resolve automaticamente o CreateLancamentoInputValidator via DI
        var validator = httpContext.RequestServices.GetRequiredService<CreateLancamentoInputValidator>();
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        return input;
    }
}
