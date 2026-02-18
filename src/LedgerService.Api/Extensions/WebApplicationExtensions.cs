using Asp.Versioning.ApiExplorer;

namespace LedgerService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    url: $"/swagger/{description.GroupName}/swagger.json",
                    name: $"LedgerService API {description.GroupName}{(description.IsDeprecated ? " (DEPRECATED)" : string.Empty)}");
            }

            options.RoutePrefix = string.Empty;
        });

        return app;
    }
}
