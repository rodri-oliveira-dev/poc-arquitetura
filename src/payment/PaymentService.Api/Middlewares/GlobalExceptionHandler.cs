using ApiDefaults.Middlewares;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using PaymentService.Api.Contracts.Responses;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Domain.Exceptions;

namespace PaymentService.Api.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : GlobalExceptionHandlerBase<GlobalExceptionHandler, ValidationErrorResponse>(
        logger,
        ValidationErrorResponseFactory.Create,
        ValidationErrorResponseFactory.Create)
{
    protected override bool IsHandledException(Exception exception)
        => exception is FluentValidation.ValidationException or ForbiddenException or ConflictException or NotFoundException or DomainException
            || IsPaymentUniqueViolation(exception);

    protected override (int statusCode, string title, string detail) MapException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Invalid request", "One or more validation errors occurred."),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            _ when IsPaymentUniqueViolation(exception) => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "Payment ja registrado para a chave de idempotencia informada."),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso nao encontrado", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Violacao de regra de dominio", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };
    }

    private static bool IsPaymentUniqueViolation(Exception exception)
        => exception is DbUpdateException { InnerException: PostgresException postgresException }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                "ux_payment_idempotency_merchant_key",
                StringComparison.Ordinal);
}
