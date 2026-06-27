using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using IdentityService.Api.Swagger;

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

    private sealed class FakeApiVersionDescriptionProvider(IReadOnlyList<ApiVersionDescription> descriptions)
        : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions => descriptions;
    }
}
