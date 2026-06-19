using ApiDefaults.Middlewares;

using BalanceService.Api.Contracts;
using BalanceService.Application.Common.Exceptions;
using BalanceService.Domain.Exceptions;

namespace BalanceService.Api.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : GlobalExceptionHandlerBase<GlobalExceptionHandler, ValidationErrorResponse>(
        logger,
        ValidationErrorResponseFactory.Create,
        ValidationErrorResponseFactory.Create)
{
    protected override bool IsHandledException(Exception exception)
        => exception is FluentValidation.ValidationException or ConflictException or NotFoundException or DomainException;

    protected override (int statusCode, string title, string detail) MapException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Invalid request", "One or more validation errors occurred."),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Violação de regra de domínio", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };
    }
}
