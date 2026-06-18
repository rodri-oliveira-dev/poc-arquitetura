using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using TransferService.Api.Swagger;

namespace TransferService.IntegrationTests.Api.Swagger;

public sealed class AuthorizeOperationFilterTests
{
    [Fact]
    public void Apply_should_add_security_requirement_when_endpoint_requires_authorization()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };
        var context = CreateContext(typeof(SecuredController).GetMethod(nameof(SecuredController.Get))!);

        sut.Apply(operation, context);

        var requirement = Assert.Single(operation.Security!);
        Assert.Contains(requirement.Keys, scheme => scheme.Reference.Id == "Bearer");
        Assert.Contains("transfer.read", operation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_should_leave_operation_unchanged_without_authorization_metadata()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };
        var context = CreateContext(typeof(OpenController).GetMethod(nameof(OpenController.Get))!);

        sut.Apply(operation, context);

        Assert.Null(operation.Security);
        Assert.Equal("desc", operation.Description);
    }

    [Fact]
    public void Apply_should_skip_allow_anonymous_endpoint()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };
        var context = CreateContext(typeof(AnonymousController).GetMethod(nameof(AnonymousController.Get))!);

        sut.Apply(operation, context);

        Assert.Null(operation.Security);
        Assert.Equal("desc", operation.Description);
    }

    [Fact]
    public void Apply_should_validate_arguments()
    {
        var sut = new AuthorizeOperationFilter();
        var context = CreateContext(typeof(OpenController).GetMethod(nameof(OpenController.Get))!);

        Assert.Throws<ArgumentNullException>(() => sut.Apply(null!, context));
        Assert.Throws<ArgumentNullException>(() => sut.Apply(new OpenApiOperation(), null!));
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var apiDescription = new ApiDescription { RelativePath = "transferencias" };
        return new OperationFilterContext(
            apiDescription,
            new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions())),
            new SchemaRepository(),
            new OpenApiDocument(),
            method);
    }

    [Authorize(Policy = "scope:transfer.read")]
    private sealed class SecuredController
    {
        public static void Get()
        {
        }
    }

    private sealed class OpenController
    {
        public static void Get()
        {
        }
    }

    private sealed class AnonymousController
    {
        [AllowAnonymous]
        [Authorize(Policy = "scope:transfer.read")]
        public static void Get()
        {
        }
    }
}
