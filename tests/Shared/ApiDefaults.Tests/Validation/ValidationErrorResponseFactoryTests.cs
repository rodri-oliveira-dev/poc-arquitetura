using ApiDefaults.Middlewares;
using ApiDefaults.Validation;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace ApiDefaults.Tests.Validation;

public sealed class ValidationErrorResponseFactoryTests
{
    [Fact]
    public void Create_should_group_validation_errors_and_normalize_field_names()
    {
        var context = CreateContext();
        var exception = new ValidationException(
        [
            new ValidationFailure("request.Amount", "Amount is required."),
            new ValidationFailure("$.SourceMerchantId", "Source merchant is required."),
            new ValidationFailure("request", "Request body is required.")
        ]);

        var response = ValidationErrorResponseFactory.Create(context, exception, CreateResponse);

        Assert.Equal("correlation-1", response.CorrelationId);
        Assert.Contains("amount", response.Errors.Keys);
        Assert.Contains("sourceMerchantId", response.Errors.Keys);
        Assert.Contains("$", response.Errors.Keys);
    }

    [Fact]
    public void CreateResult_should_convert_model_state_errors_to_bad_request()
    {
        var httpContext = CreateContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        actionContext.ModelState.AddModelError("request.Amount", "Amount must be greater than zero.");

        var result = Assert.IsType<BadRequestObjectResult>(
            ValidationErrorResponseFactory.CreateResult(actionContext, CreateResponse));

        Assert.Contains("application/json", result.ContentTypes);
        var response = Assert.IsType<ValidationErrorResponse>(result.Value);
        Assert.Contains("amount", response.Errors.Keys);
    }

    [Fact]
    public void Create_should_preserve_multiple_errors_for_the_same_field()
    {
        var context = CreateContext();
        var exception = new ValidationException(
        [
            new ValidationFailure("request.Amount", "Amount is required."),
            new ValidationFailure("Amount", "Amount must be greater than zero.")
        ]);

        var response = ValidationErrorResponseFactory.Create(context, exception, CreateResponse);

        Assert.Collection(
            response.Errors["amount"],
            message => Assert.Equal("Amount is required.", message),
            message => Assert.Equal("Amount must be greater than zero.", message));
    }

    [Fact]
    public void Create_should_support_empty_field_and_empty_message_as_global_error()
    {
        var context = CreateContext();
        var exception = new ValidationException([new ValidationFailure("", "")]);

        var response = ValidationErrorResponseFactory.Create(context, exception, CreateResponse);

        string message = Assert.Single(response.Errors["$"]);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void CreateResult_should_use_default_message_when_model_error_message_is_empty()
    {
        var httpContext = CreateContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        actionContext.ModelState.AddModelError("request", string.Empty);

        var result = Assert.IsType<BadRequestObjectResult>(
            ValidationErrorResponseFactory.CreateResult(actionContext, CreateResponse));

        var response = Assert.IsType<ValidationErrorResponse>(result.Value);
        string message = Assert.Single(response.Errors["$"]);
        Assert.Equal("The input was not valid.", message);
    }

    [Fact]
    public void Create_should_create_single_field_error_from_json_validation_path()
    {
        var context = CreateContext();

        var response = ValidationErrorResponseFactory.Create(context, "$.Items[0].Amount", "Invalid amount.", CreateResponse);

        string message = Assert.Single(response.Errors["items[0].amount"]);
        Assert.Equal("Invalid amount.", message);
        Assert.Equal("correlation-1", response.CorrelationId);
    }

    [Fact]
    public void Create_should_validate_public_arguments()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.Create(null!, new ValidationException([]), CreateResponse));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.Create(context, null!, CreateResponse));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.Create<ValidationErrorResponse>(context, new ValidationException([]), null!));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.CreateResult(null!, CreateResponse));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.CreateResult<ValidationErrorResponse>(null!, null!));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.Create(null!, "$", "message", CreateResponse));
        Assert.Throws<ArgumentNullException>(
            () => ValidationErrorResponseFactory.Create<ValidationErrorResponse>(context, "$", "message", null!));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "correlation-1";
        return context;
    }

    private static ValidationErrorResponse CreateResponse(Dictionary<string, string[]> errors, string? correlationId)
        => new(errors, correlationId);

    private sealed record ValidationErrorResponse(Dictionary<string, string[]> Errors, string? CorrelationId);
}
