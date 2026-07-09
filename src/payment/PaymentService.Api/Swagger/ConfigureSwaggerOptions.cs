using Asp.Versioning.ApiExplorer;

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentService.Api.Swagger;

public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddServer(new OpenApiServer
        {
            Url = "http://localhost:5234",
            Description = "Ambiente local direto do PaymentService.Api"
        });

        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Poc Arquitetura Payment API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "API em Clean Architecture para payments externos (DEPRECATED)"
                    : "API em Clean Architecture para payments externos"
            });
        }
    }
}
