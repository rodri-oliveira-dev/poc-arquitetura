using Asp.Versioning.ApiExplorer;

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuditService.Api.Swagger;

/// <summary>
/// Configura documentos Swagger/OpenAPI por versao da API.
/// </summary>
public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.AddServer(new OpenApiServer
        {
            Url = "/",
            Description = "Servidor relativo do AuditService.Api"
        });

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Poc Arquitetura Audit API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "Bounded context de auditoria funcional (DEPRECATED)"
                    : "Bounded context de auditoria funcional"
            });
        }
    }
}
