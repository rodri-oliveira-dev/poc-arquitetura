using System.Reflection;
using LedgerService.Api.Controllers;
using LedgerService.Api.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace LedgerService.UnitTests.Api.Swagger;

public sealed class LancamentosExamplesOperationFilterTests
{
    [Fact]
    public void Apply_should_add_request_and_response_examples_for_create()
    {
        var sut = new LancamentosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(LancamentosController).GetMethod(nameof(LancamentosController.Create))!);

        sut.Apply(operation, context);

        var requestExample = Assert.IsType<JsonObject>(operation.RequestBody!.Content["application/json"].Example);
        Assert.Equal("tese", requestExample["merchantId"]!.GetValue<string>());
        Assert.Equal("CREDIT", requestExample["type"]!.GetValue<string>());
        var createdExample = Assert.IsType<JsonObject>(operation.Responses["201"].Content["application/json"].Example);
        Assert.Equal("lan_9f3a1b2c", createdExample["id"]!.GetValue<string>());
        var badRequestExample = Assert.IsType<JsonObject>(operation.Responses["400"].Content["application/json"].Example);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestExample["status"]!.GetValue<int>());
    }

    [Fact]
    public void Apply_should_skip_methods_outside_lancamentos_create()
    {
        var sut = new LancamentosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(OtherController).GetMethod(nameof(OtherController.Get))!);

        sut.Apply(operation, context);

        Assert.Null(operation.RequestBody!.Content["application/json"].Example);
        Assert.Null(operation.Responses["201"].Content["application/json"].Example);
        Assert.Null(operation.Responses["400"].Content["application/json"].Example);
    }

    private static OpenApiOperation CreateOperation()
    {
        return new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
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
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType()
            }
        };
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var apiDescription = new ApiDescription { RelativePath = "x" };
        return new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions())), new SchemaRepository(), new OpenApiDocument(), method);
    }

    private sealed class OtherController
    {
        public static void Get()
        {
        }
    }
}
