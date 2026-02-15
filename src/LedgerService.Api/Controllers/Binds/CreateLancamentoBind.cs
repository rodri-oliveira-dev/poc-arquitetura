using FluentValidation;
using LedgerService.Api.Contracts;
using LedgerService.Api.Middlewares;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;
using Microsoft.Extensions.DependencyInjection;

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
        // NORMALIZAÇÃO
        // - OccurredAt: se vazio -> utcNow
        // - Type: case-insensitive e normaliza para canônico (UPPER)
        // - CorrelationId: se vier vazio, pega do middleware (já garante GUID)
        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        var normalizedType = (request.Type ?? string.Empty).Trim().ToUpperInvariant();

        // OccurredAt: o contrato HTTP atual (CreateLancamentoRequest) não possui esse campo.
        // Então definimos o valor como utcNow em formato ISO-8601.
        // Observação: a regra de negócio atualmente usa DateTime.Now no service.
        // Aqui apenas garantimos que o input tenha um valor consistente para validação/logs/eventos.
        var normalizedOccurredAt = DateTime.UtcNow.ToString("o");

        var input = new CreateLancamentoInput(
            request.MerchantId,
            normalizedType,
            request.Amount,
            normalizedOccurredAt,
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
