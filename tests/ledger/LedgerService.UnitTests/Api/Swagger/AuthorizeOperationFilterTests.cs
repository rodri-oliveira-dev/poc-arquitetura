using System.Reflection;

using LedgerService.Api.Swagger;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace LedgerService.UnitTests.Api.Swagger;

public sealed class AuthorizeOperationFilterTests
{
    [Fact]
    public void Apply_should_skip_when_allowanonymous()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(AnonymousController).GetMethod(nameof(AnonymousController.Get))!);

        sut.Apply(operation, ctx);
        Assert.Null(operation.Security);
    }

    [Fact]
    public void Apply_should_add_security_and_description_when_authorized_with_scope_policy()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(SecuredController).GetMethod(nameof(SecuredController.Post))!);

        sut.Apply(operation, ctx);
        Assert.NotNull(operation.Security);
        Assert.Contains("requer scope", operation.Description);
        Assert.Contains("ledger.write", operation.Description);
    }

    [Fact]
    public void Apply_should_not_change_description_when_authorize_has_no_policy()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(NoPolicyController).GetMethod(nameof(NoPolicyController.Get))!);

        sut.Apply(operation, ctx);
        Assert.NotNull(operation.Security);
        Assert.Equal("desc", operation.Description);
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var apiDescription = new ApiDescription { RelativePath = "x" };
        return new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions())), new SchemaRepository(), new OpenApiDocument(), method);
    }

    private sealed class AnonymousController
    {
        [AllowAnonymous]
        public void Get()
        {
        }
    }

    [Authorize(Policy = "scope:ledger.write")]
    private sealed class SecuredController
    {
        public void Post()
        {
        }
    }

    private sealed class NoPolicyController
    {
        [Authorize]
        public void Get()
        {
        }
    }
}
