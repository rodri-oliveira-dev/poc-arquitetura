using Auth.Api.Swagger;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Reflection;
using System.Text.Json;

namespace Auth.UnitTests.Swagger;

public sealed class LoginOperationFilterTests
{
    [Fact]
    public void Apply_should_do_nothing_for_other_paths()
    {
        var sut = new LoginOperationFilter();
        var operation = new OpenApiOperation
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
                ["200"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } },
                ["400"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } },
                ["401"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } }
            }
        };

        var context = CreateContext(relativePath: "health");

        sut.Apply(operation, context);
        Assert.Null(operation.Extensions);
        Assert.Null(operation.RequestBody.Content["application/json"].Example);
    }

    [Fact]
    public void Apply_should_enrich_operation_for_auth_login_path()
    {
        var sut = new LoginOperationFilter();
        var operation = new OpenApiOperation
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
                ["200"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } },
                ["400"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } },
                ["401"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() } }
            }
        };

        var context = CreateContext(relativePath: "auth/login");

        sut.Apply(operation, context);
        Assert.True(operation.Extensions.ContainsKey("x-valid-scopes"));
        Assert.NotNull(operation.RequestBody.Content["application/json"].Example);
        Assert.NotNull(operation.Responses["200"].Content["application/json"].Example);
        Assert.NotNull(operation.Responses["400"].Content["application/json"].Example);
        Assert.NotNull(operation.Responses["401"].Content["application/json"].Example);
    }

    private static OperationFilterContext CreateContext(string relativePath)
    {
        var apiDescription = new ApiDescription
        {
            RelativePath = relativePath
        };

        // OperationFilterContext exige MethodInfo e esquema, mas o filtro usa apenas ApiDescription.RelativePath.
        // Então usamos um método fake.
        var method = typeof(LoginOperationFilterTests).GetMethod(nameof(Dummy), BindingFlags.NonPublic | BindingFlags.Static)!;

        return new OperationFilterContext(apiDescription, new SchemaGenerator(new SchemaGeneratorOptions(), new JsonSerializerDataContractResolver(new JsonSerializerOptions())), new SchemaRepository(), new OpenApiDocument(), method);
    }

    private static void Dummy()
    {
    }
}
