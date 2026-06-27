using System.Reflection;

using IdentityService.Api.Security;
using IdentityService.Api.Swagger;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.UnitTests.Api.Swagger;

public sealed class AuthorizeOperationFilterTests
{
    [Fact]
    public void Apply_should_add_bearer_security_and_scope_description_for_authorized_endpoint()
    {
        OpenApiOperation operation = new()
        {
            Description = "Cria usuario."
        };
        var context = CreateContext(
            new AuthorizeAttribute
            {
                Policy = ScopePolicies.IdentityWritePolicy
            });
        var filter = new AuthorizeOperationFilter();

        filter.Apply(operation, context);

        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        Assert.Contains("identity.write", operation.Description, StringComparison.Ordinal);
        Assert.Contains("Cria usuario.", operation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_should_keep_operation_unchanged_when_endpoint_allows_anonymous()
    {
        OpenApiOperation operation = new();
        var context = CreateContext(
            new AllowAnonymousAttribute(),
            new AuthorizeAttribute
            {
                Policy = ScopePolicies.IdentityWritePolicy
            });
        var filter = new AuthorizeOperationFilter();

        filter.Apply(operation, context);

        Assert.Null(operation.Security);
        Assert.Null(operation.Description);
    }

    [Fact]
    public void Apply_should_keep_operation_unchanged_without_authorize_metadata()
    {
        OpenApiOperation operation = new();
        var context = CreateContext();
        var filter = new AuthorizeOperationFilter();

        filter.Apply(operation, context);

        Assert.Null(operation.Security);
        Assert.Null(operation.Description);
    }

    [Fact]
    public void Apply_should_add_security_without_scope_description_when_policy_is_not_scope_based()
    {
        OpenApiOperation operation = new();
        var context = CreateContext(
            new AuthorizeAttribute
            {
                Policy = "custom-policy"
            });
        var filter = new AuthorizeOperationFilter();

        filter.Apply(operation, context);

        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        Assert.Null(operation.Description);
    }

    private static OperationFilterContext CreateContext(params object[] metadata)
    {
        ApiDescription apiDescription = new()
        {
            ActionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = [.. metadata]
            }
        };

        return new OperationFilterContext(
            apiDescription,
            null,
            null,
            new OpenApiDocument(),
            typeof(AuthorizeOperationFilterTests).GetMethod(
                nameof(DummyEndpoint),
                BindingFlags.NonPublic | BindingFlags.Static));
    }

    private static void DummyEndpoint()
    {
    }
}
