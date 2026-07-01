using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using IdentityService.Api.Swagger;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.UnitTests.Api.Swagger;

public sealed class ConfigureSwaggerOptionsTests
{
    [Fact]
    public void Configure_should_add_local_server_and_versioned_documents()
    {
        var sut = new ConfigureSwaggerOptions(new FakeApiVersionDescriptionProvider(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", true, null, null)
        ]));
        var options = new SwaggerGenOptions();

        sut.Configure(options);

        var server = Assert.Single(options.SwaggerGeneratorOptions.Servers);
        Assert.Equal("http://localhost:5229", server.Url);
        Assert.Equal("Ambiente local direto do IdentityService.Api", server.Description);
        Assert.Equal("Poc Arquitetura Identity API", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
        Assert.Equal("1.0", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Version);
        Assert.Equal("2.0", options.SwaggerGeneratorOptions.SwaggerDocs["v2"].Version);
    }

    [Fact]
    public void Configure_should_validate_arguments()
    {
        var sut = new ConfigureSwaggerOptions(new FakeApiVersionDescriptionProvider([]));

        Assert.Throws<ArgumentNullException>(() => sut.Configure(null!));
    }

    [Fact]
    public void CreateUserIdempotencyHeaderOperationFilter_should_document_optional_header()
    {
        var operation = new OpenApiOperation
        {
            OperationId = "CreateIdentityUser"
        };
        var sut = new CreateUserIdempotencyHeaderOperationFilter();

        sut.Apply(operation, null!);

        Assert.NotNull(operation.Parameters);
        var parameter = Assert.Single(operation.Parameters);
        Assert.Equal("Idempotency-Key", parameter.Name);
        Assert.Equal(ParameterLocation.Header, parameter.In);
        Assert.False(parameter.Required);
        Assert.NotNull(parameter.Schema);
        Assert.Equal(JsonSchemaType.String, parameter.Schema.Type);
        Assert.Equal(1, parameter.Schema.MinLength);
        Assert.Equal(128, parameter.Schema.MaxLength);
        Assert.Equal("^[A-Za-z0-9._:-]{1,128}$", parameter.Schema.Pattern);
        Assert.Contains("opcional", parameter.Description, StringComparison.Ordinal);
    }

    private sealed class FakeApiVersionDescriptionProvider(IReadOnlyList<ApiVersionDescription> descriptions)
        : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions => descriptions;
    }
}
