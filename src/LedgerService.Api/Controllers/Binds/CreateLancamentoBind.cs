using FluentValidation;
using FluentValidation.Results;
using LedgerService.Api.Contracts;
using LedgerService.Api.Mappers;
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
        ValidateTransportHeaders(idempotencyKey);

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            : correlationId;

        var input = CreateLancamentoInputMapper.ToInput(request, idempotencyKey, resolvedCorrelationId);

        // Validação de request (payload normalizado), sem regras estritamente de transporte HTTP.
        var validator = httpContext.RequestServices.GetRequiredService<IValidator<CreateLancamentoInput>>();
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        return input;
    }

    private static void ValidateTransportHeaders(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(CreateLancamentoInput.IdempotencyKey), "Idempotency-Key is required.")
            });
        }

        if (!Guid.TryParse(idempotencyKey, out _))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(CreateLancamentoInput.IdempotencyKey), "Idempotency-Key must be a valid UUID.")
            });
        }
    }
}
