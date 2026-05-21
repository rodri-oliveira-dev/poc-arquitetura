using System.Reflection;
using BalanceService.Api.Controllers;
using BalanceService.Api.Swagger;
using FluentAssertions;
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

        var successExample = operation.Responses["200"].Content["application/json"].Example
            .Should().BeOfType<JsonObject>().Subject;
        successExample["date"]!.GetValue<string>().Should().Be("2026-02-14");
        successExample["netBalance"]!.GetValue<string>().Should().Be("150.00");

        var errorExample = operation.Responses["400"].Content["application/json"].Example
            .Should().BeOfType<JsonObject>().Subject;
        errorExample["status"]!.GetValue<int>().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Apply_should_add_period_examples_for_get_period()
    {
        var sut = new ConsolidadosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(ConsolidadosController).GetMethod(nameof(ConsolidadosController.GetPeriod))!);

        sut.Apply(operation, context);

        var successExample = operation.Responses["200"].Content["application/json"].Example
            .Should().BeOfType<JsonObject>().Subject;
        successExample["from"]!.GetValue<string>().Should().Be("2026-02-10");
        successExample["items"].Should().BeOfType<JsonArray>().Which.Should().HaveCount(2);

        var errorExample = operation.Responses["400"].Content["application/json"].Example
            .Should().BeOfType<JsonObject>().Subject;
        errorExample["title"]!.GetValue<string>().Should().Be("Invalid request");
    }

    [Fact]
    public void Apply_should_skip_methods_outside_consolidados_controller()
    {
        var sut = new ConsolidadosExamplesOperationFilter();
        var operation = CreateOperation();
        var context = CreateContext(typeof(OtherController).GetMethod(nameof(OtherController.Get))!);

        sut.Apply(operation, context);

        operation.Responses["200"].Content["application/json"].Example.Should().BeNull();
        operation.Responses["400"].Content["application/json"].Example.Should().BeNull();
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
        public static void Get() { }
    }
}
