using System.Reflection;
using FluentAssertions;
using LedgerService.Api.Controllers;
using LedgerService.Api.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LedgerService.UnitTests.Tests;

public sealed class LancamentosExamplesOperationFilterTests
{
    [Fact]
    public void Apply_should_add_request_and_response_examples_for_create()
    {
        var sut = new LancamentosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(LancamentosController).GetMethod(nameof(LancamentosController.Create))!);

        sut.Apply(operation, context);

        var requestExample = operation.RequestBody!.Content["application/json"].Example
            .Should().BeOfType<OpenApiObject>().Subject;
        requestExample["merchantId"].Should().BeOfType<OpenApiString>().Which.Value.Should().Be("tese");
        requestExample["type"].Should().BeOfType<OpenApiString>().Which.Value.Should().Be("CREDIT");

        var createdExample = operation.Responses["201"].Content["application/json"].Example
            .Should().BeOfType<OpenApiObject>().Subject;
        createdExample["id"].Should().BeOfType<OpenApiString>().Which.Value.Should().Be("lan_9f3a1b2c");

        var badRequestExample = operation.Responses["400"].Content["application/json"].Example
            .Should().BeOfType<OpenApiObject>().Subject;
        badRequestExample["status"].Should().BeOfType<OpenApiInteger>().Which.Value.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Apply_should_skip_methods_outside_lancamentos_create()
    {
        var sut = new LancamentosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(OtherController).GetMethod(nameof(OtherController.Get))!);

        sut.Apply(operation, context);

        operation.RequestBody!.Content["application/json"].Example.Should().BeNull();
        operation.Responses["201"].Content["application/json"].Example.Should().BeNull();
        operation.Responses["400"].Content["application/json"].Example.Should().BeNull();
    }

    private static OpenApiOperation CreateOperation()
    {
        return new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            },
            Responses = new OpenApiResponses
            {
                ["201"] = CreateJsonResponse(),
                ["400"] = CreateJsonResponse()
            }
        };
    }

    private static OpenApiResponse CreateJsonResponse()
    {
        return new OpenApiResponse
        {
            Content =
            {
                ["application/json"] = new OpenApiMediaType()
            }
        };
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var apiDescription = new ApiDescription { RelativePath = "x" };
        return new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions())), new SchemaRepository(), method);
    }

    private sealed class OtherController
    {
        public static void Get() { }
    }
}
