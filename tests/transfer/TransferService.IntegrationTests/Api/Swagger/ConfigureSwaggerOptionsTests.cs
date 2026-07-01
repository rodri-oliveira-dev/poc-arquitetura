using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using TransferService.Api.Extensions;
using TransferService.Api.Swagger;

namespace TransferService.IntegrationTests.Api.Swagger;

public sealed class ConfigureSwaggerOptionsTests
{
    [Fact]
    public void Configure_should_add_server_and_versioned_documents()
    {
        var sut = new ConfigureSwaggerOptions(new FakeApiVersionDescriptionProvider(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", true, null, null)
        ]));
        var options = new SwaggerGenOptions();

        sut.Configure(options);

        var server = Assert.Single(options.SwaggerGeneratorOptions.Servers);
        Assert.Equal("http://localhost:5230", server.Url);
        Assert.Equal("Poc Arquitetura Transfer API", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
        Assert.Equal("1.0", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Version);
        Assert.Contains("DEPRECATED", options.SwaggerGeneratorOptions.SwaggerDocs["v2"].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_should_validate_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigureSwaggerOptions(null!));
        var sut = new ConfigureSwaggerOptions(new FakeApiVersionDescriptionProvider([]));
        Assert.Throws<ArgumentNullException>(() => sut.Configure(null!));
    }

    [Fact]
    public void AddApiSwagger_should_configure_bearer_jwt_security_scheme()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApiVersionDescriptionProvider>(new FakeApiVersionDescriptionProvider(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null)
        ]));

        services.AddApiSwagger();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;

        var schemes = options.SwaggerGeneratorOptions.SecuritySchemes;
        Assert.Equal(SecuritySchemeType.Http, schemes["Bearer"].Type);
        Assert.Equal("bearer", schemes["Bearer"].Scheme);
        Assert.Equal("JWT", schemes["Bearer"].BearerFormat);
        Assert.DoesNotContain("Idempotency-Key", schemes.Keys);
        Assert.Contains(options.OperationFilterDescriptors, descriptor => descriptor.Type == typeof(AuthorizeOperationFilter));
    }

    private sealed class FakeApiVersionDescriptionProvider(IReadOnlyList<ApiVersionDescription> descriptions)
        : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions => descriptions;
    }
}
