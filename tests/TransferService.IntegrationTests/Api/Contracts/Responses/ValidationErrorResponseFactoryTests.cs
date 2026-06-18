using ApiDefaults.Middlewares;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

using TransferService.Api.Contracts.Responses;

namespace TransferService.IntegrationTests.Api.Contracts.Responses;

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

        var response = ValidationErrorResponseFactory.Create(context, exception);

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

        var result = Assert.IsType<BadRequestObjectResult>(ValidationErrorResponseFactory.CreateResult(actionContext));

        Assert.Contains("application/json", result.ContentTypes);
        var response = Assert.IsType<ValidationErrorResponse>(result.Value);
        Assert.Contains("amount", response.Errors.Keys);
    }

    [Fact]
    public void Create_should_validate_public_arguments()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => ValidationErrorResponseFactory.Create(null!, new ValidationException([])));
        Assert.Throws<ArgumentNullException>(() => ValidationErrorResponseFactory.Create(context, null!));
        Assert.Throws<ArgumentNullException>(() => ValidationErrorResponseFactory.CreateResult(null!));
        Assert.Throws<ArgumentNullException>(() => ValidationErrorResponseFactory.Create(null!, "$", "message"));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "correlation-1";
        return context;
    }
}
