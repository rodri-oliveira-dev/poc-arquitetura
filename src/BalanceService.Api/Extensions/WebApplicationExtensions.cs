using Asp.Versioning.ApiExplorer;

namespace BalanceService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!IsSwaggerEnabled(app.Environment, configuration))
            return app;

        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    url: $"/swagger/{description.GroupName}/swagger.json",
                    name: $"BalanceService API {description.GroupName}{(description.IsDeprecated ? " (DEPRECATED)" : string.Empty)}");
            }

            options.RoutePrefix = string.Empty;
        });

        return app;
    }

    public static bool IsSwaggerEnabled(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        return environment.IsDevelopment() || configuration.GetValue<bool>("Swagger:Enabled");
    }
}
