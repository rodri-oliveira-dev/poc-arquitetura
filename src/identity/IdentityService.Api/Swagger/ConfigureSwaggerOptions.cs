using Asp.Versioning.ApiExplorer;

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityService.Api.Swagger;

public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddServer(new OpenApiServer
        {
            Url = "http://localhost:5229",
            Description = "Ambiente local direto do IdentityService.Api"
        });

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Poc Arquitetura Identity API",
                Version = description.ApiVersion.ToString(),
                Description = "API de cadastro de usuarios do IdentityService"
            });
        }
    }
}
