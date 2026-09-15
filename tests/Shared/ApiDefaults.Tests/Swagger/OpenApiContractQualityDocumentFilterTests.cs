using System.Text.Json.Nodes;

using ApiDefaults.Swagger;

using Microsoft.OpenApi;

using Moq;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiDefaults.Tests.Swagger;

public sealed class OpenApiContractQualityDocumentFilterTests
{
    [Fact]
    public void Apply_WhenDocumentIsNull_ShouldThrow()
    {
        var filter = new OpenApiContractQualityDocumentFilter();

        Assert.Throws<ArgumentNullException>("swaggerDoc", () =>
            filter.Apply(null!, CreateContext()));
    }

    [Fact]
    public void Apply_WhenTagsAreNull_ShouldReturnWithoutChangingOperations()
    {
        OpenApiOperation operation = CreateSecuredOperation();
        OpenApiDocument document = new()
        {
            Tags = null,
            Paths = CreatePaths("/health", operation)
        };

        new OpenApiContractQualityDocumentFilter().Apply(document, CreateContext());

        Assert.NotEmpty(operation.Security);
    }

    [Fact]
    public void Apply_ShouldDescribeKnownTagsAndPreserveUnknownTags()
    {
        OpenApiDocument document = new()
        {
            Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Lancamentos", Description = "valor anterior" },
                new() { Name = "Operacional" },
                new() { Name = "Custom", Description = "mantido" },
                new()
            }
        };

        new OpenApiContractQualityDocumentFilter().Apply(document, CreateContext());

        Assert.Equal("Operacoes de escrita, estorno, reprocessamento e consulta de status do ledger.", document.Tags.Single(tag => tag.Name == "Lancamentos").Description);
        Assert.Equal("Endpoints operacionais publicos de liveness e readiness.", document.Tags.Single(tag => tag.Name == "Operacional").Description);
        Assert.Equal("mantido", document.Tags.Single(tag => tag.Name == "Custom").Description);
        Assert.Null(document.Tags.Single(tag => tag.Name is null).Description);
    }

    [Fact]
    public void Apply_ShouldMarkHealthAndReadyAsPublicAndPreserveOtherOperations()
    {
        OpenApiOperation health = CreateSecuredOperation();
        OpenApiOperation ready = CreateSecuredOperation();
        OpenApiOperation secure = CreateSecuredOperation();
        OpenApiDocument document = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = new OpenApiPaths
            {
                ["/health"] = CreatePathItem(health),
                ["/ready"] = CreatePathItem(ready),
                ["/v1/secure"] = CreatePathItem(secure)
            }
        };

        new OpenApiContractQualityDocumentFilter().Apply(document, CreateContext());

        Assert.Empty(health.Security);
        Assert.Empty(ready.Security);
        Assert.NotEmpty(secure.Security);
    }

    [Fact]
    public void Apply_WhenCalledTwice_ShouldRemainIdempotent()
    {
        OpenApiOperation health = CreateSecuredOperation();
        OpenApiDocument document = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = CreatePaths("/health", health)
        };
        var filter = new OpenApiContractQualityDocumentFilter();

        filter.Apply(document, CreateContext());
        filter.Apply(document, CreateContext());

        Assert.Empty(health.Security);
        Assert.Equal("Endpoints operacionais publicos de liveness e readiness.", document.Tags.Single().Description);
    }

    [Fact]
    public void Apply_WhenPathsOrOperationsAreMissing_ShouldNotThrow()
    {
        var filter = new OpenApiContractQualityDocumentFilter();
        OpenApiDocument withoutPaths = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = null
        };
        OpenApiDocument withoutMatchingPath = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = CreatePaths("/other", CreateSecuredOperation())
        };
        OpenApiDocument withoutOperations = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = new OpenApiPaths
            {
                ["/health"] = new OpenApiPathItem()
            }
        };

        filter.Apply(withoutPaths, CreateContext());
        filter.Apply(withoutMatchingPath, CreateContext());
        filter.Apply(withoutOperations, CreateContext());

        Assert.Equal("Endpoints operacionais publicos de liveness e readiness.", withoutPaths.Tags.Single().Description);
    }

    [Fact]
    public void Apply_ShouldPreserveOperationContractDetails()
    {
        OpenApiOperation health = CreateSecuredOperation();
        health.OperationId = "Health_Get";
        health.Responses ??= [];
        health.Parameters =
        [
            new OpenApiParameter
            {
                Name = "verbose",
                In = ParameterLocation.Query,
                Description = "Inclui detalhes resumidos.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean | JsonSchemaType.Null }
            }
        ];
        health.Responses["200"] = new OpenApiResponse
        {
            Description = "OK",
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["X-Correlation-Id"] = new OpenApiHeader
                {
                    Description = "Correlation ID propagado.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            },
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["text/plain"] = new OpenApiMediaType
                {
                    Example = JsonValue.Create("ok")
                }
            }
        };
        OpenApiDocument document = new()
        {
            Tags = new HashSet<OpenApiTag> { new() { Name = "Operacional" } },
            Paths = CreatePaths("/health", health),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Status"] = new OpenApiSchema
                    {
                        Description = "Status operacional.",
                        Type = JsonSchemaType.String,
                        Enum = [JsonValue.Create("ready")!, JsonValue.Create("not_ready")!]
                    }
                }
            }
        };

        new OpenApiContractQualityDocumentFilter().Apply(document, CreateContext());

        Assert.Equal("Health_Get", health.OperationId);
        Assert.Equal("Inclui detalhes resumidos.", health.Parameters.Single().Description);
        Assert.True((health.Parameters.Single().Schema.Type.GetValueOrDefault() & JsonSchemaType.Null) == JsonSchemaType.Null);
        Assert.Equal("OK", health.Responses["200"].Description);
        Assert.True(health.Responses["200"].Headers.ContainsKey("X-Correlation-Id"));
        Assert.Equal("Status operacional.", document.Components.Schemas["Status"].Description);
        Assert.Equal(2, document.Components.Schemas["Status"].Enum.Count);
        Assert.Empty(health.Security);
    }

    private static OpenApiPaths CreatePaths(string path, OpenApiOperation operation)
        => new()
        {
            [path] = CreatePathItem(operation)
        };

    private static OpenApiPathItem CreatePathItem(OpenApiOperation operation)
        => new()
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = operation
            }
        };

    private static OpenApiOperation CreateSecuredOperation()
        => new()
        {
            Security =
            [
                []
            ],
            Responses = []
        };

    private static DocumentFilterContext CreateContext()
        => new([], Mock.Of<ISchemaGenerator>(), new SchemaRepository());
}
