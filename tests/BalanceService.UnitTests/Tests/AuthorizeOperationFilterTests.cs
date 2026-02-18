using System.Reflection;
using BalanceService.Api.Swagger;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BalanceService.UnitTests.Tests;

public sealed class AuthorizeOperationFilterTests
{
    [Fact]
    public void Apply_should_skip_when_allowanonymous()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(AnonymousController).GetMethod(nameof(AnonymousController.Get))!);

        sut.Apply(operation, ctx);

        operation.Security.Should().BeEmpty();
    }

    [Fact]
    public void Apply_should_add_security_and_description_when_authorized_with_scope_policy()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(SecuredController).GetMethod(nameof(SecuredController.Post))!);

        sut.Apply(operation, ctx);

        operation.Security.Should().NotBeNull();
        operation.Description.Should().Contain("requer scope");
        operation.Description.Should().Contain("balance.read");
    }

    [Fact]
    public void Apply_should_not_change_description_when_authorize_has_no_policy()
    {
        var sut = new AuthorizeOperationFilter();
        var operation = new OpenApiOperation { Description = "desc" };

        var ctx = CreateContext(typeof(NoPolicyController).GetMethod(nameof(NoPolicyController.Get))!);

        sut.Apply(operation, ctx);

        operation.Security.Should().NotBeNull();
        operation.Description.Should().Be("desc");
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var apiDescription = new ApiDescription { RelativePath = "x" };
        return new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions())), new SchemaRepository(), method);
    }

    private sealed class AnonymousController
    {
        [AllowAnonymous]
        public void Get() { }
    }

    [Authorize(Policy = "scope:balance.read")]
    private sealed class SecuredController
    {
        public void Post() { }
    }

    private sealed class NoPolicyController
    {
        [Authorize]
        public void Get() { }
    }
}
