using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TransferService.Api.Swagger;

public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        options.AddServer(new OpenApiServer
        {
            Url = "http://localhost:5230",
            Description = "Ambiente local direto do TransferService.Api"
        });

        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Poc Arquitetura Transfer API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "API em Clean Architecture com saga de transferencias (DEPRECATED)"
                    : "API em Clean Architecture com saga de transferencias"
            });
        }
    }
}
