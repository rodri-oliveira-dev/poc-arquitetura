using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BalanceService.Api.Swagger;

/// <summary>
/// Configura documentos Swagger/OpenAPI por versão da API.
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Poc Arquitetura API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "API em Clean Architecture com foco em DDD (DEPRECATED)"
                    : "API em Clean Architecture com foco em DDD"
            });
        }
    }
}
