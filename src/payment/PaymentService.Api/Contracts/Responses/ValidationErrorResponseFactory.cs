using FluentValidation;

using Microsoft.AspNetCore.Mvc;

using SharedValidationErrorResponseFactory = ApiDefaults.Validation.ValidationErrorResponseFactory;

namespace PaymentService.Api.Contracts.Responses;

public static class ValidationErrorResponseFactory
{
    public static ValidationErrorResponse Create(HttpContext httpContext, ValidationException exception)
        => SharedValidationErrorResponseFactory.Create(httpContext, exception, CreateResponse);

    public static ValidationErrorResponse Create(HttpContext httpContext, string fieldName, string message)
        => SharedValidationErrorResponseFactory.Create(httpContext, fieldName, message, CreateResponse);

    public static IActionResult CreateResult(ActionContext context)
        => SharedValidationErrorResponseFactory.CreateResult(context, CreateResponse);

    private static ValidationErrorResponse CreateResponse(Dictionary<string, string[]> errors, string? correlationId)
        => new()
        {
            Errors = errors,
            CorrelationId = correlationId
        };
}
