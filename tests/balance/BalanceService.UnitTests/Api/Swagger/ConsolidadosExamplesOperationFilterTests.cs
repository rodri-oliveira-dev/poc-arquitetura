using System.Reflection;
using BalanceService.Api.Controllers;
using BalanceService.Api.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace BalanceService.UnitTests.Api.Swagger;

public sealed class ConsolidadosExamplesOperationFilterTests
{
    [Fact]
    public void Apply_should_add_daily_examples_for_get_daily()
    {
        var sut = new ConsolidadosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(ConsolidadosController).GetMethod(nameof(ConsolidadosController.GetDaily))!);

        sut.Apply(operation, context);

        var successExample = Assert.IsType<JsonObject>(operation.Responses["200"].Content["application/json"].Example);
        Assert.Equal("2026-02-14", successExample["date"]!.GetValue<string>());
        Assert.Equal("150.00", successExample["netBalance"]!.GetValue<string>());
        var errorExample = Assert.IsType<JsonObject>(operation.Responses["400"].Content["application/json"].Example);
        Assert.Equal(StatusCodes.Status400BadRequest, errorExample["status"]!.GetValue<int>());
    }

    [Fact]
    public void Apply_should_add_period_examples_for_get_period()
    {
        var sut = new ConsolidadosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(ConsolidadosController).GetMethod(nameof(ConsolidadosController.GetPeriod))!);

        sut.Apply(operation, context);

        var successExample = Assert.IsType<JsonObject>(operation.Responses["200"].Content["application/json"].Example);
        Assert.Equal("2026-02-10", successExample["from"]!.GetValue<string>());
        Assert.Equal(2, Assert.IsType<JsonArray>(successExample["items"]).Count);
        var errorExample = Assert.IsType<JsonObject>(operation.Responses["400"].Content["application/json"].Example);
        Assert.Equal("Invalid request", errorExample["title"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_should_skip_methods_outside_consolidados_controller()
    {
        var sut = new ConsolidadosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(OtherController).GetMethod(nameof(OtherController.Get))!);

        sut.Apply(operation, context);

        Assert.Null(operation.Responses["200"].Content["application/json"].Example);
        Assert.Null(operation.Responses["400"].Content["application/json"].Example);
    }

    private static OpenApiOperation CreateOperation()
    {
        return new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = CreateJsonResponse(),
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
