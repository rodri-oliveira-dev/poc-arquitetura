using ApiDefaults.Middlewares;
using ApiDefaults.Options;

using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Hosting;

namespace ApiDefaults.Extensions;

public static class ApiDefaultsApplicationExtensions
{
    public static IWebHostBuilder ConfigureApiDefaults(this IWebHostBuilder webHost)
    {
        return webHost.ConfigureKestrel((context, options) =>
        {
            options.AddServerHeader = false;

            long? maxRequestBodySizeBytes = context.Configuration.GetValue<long?>(
                $"{ApiDefaultsOptions.SectionName}:{nameof(ApiDefaultsOptions.MaxRequestBodySizeBytes)}");

            if (maxRequestBodySizeBytes is > 0)
            {
                options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes;
            }
        });
    }

    public static WebApplication UseApiDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsEnvironment("Test"))
        {
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<RequestBodySizeLimitMiddleware>();
        CorsOptions corsOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CorsOptions>>().Value;
        if (corsOptions.Enabled && corsOptions.AllowedOrigins.Count > 0)
        {
            app.UseCors(ApiDefaultsServiceCollectionExtensions.CorsPolicyName);
        }

        app.UseRateLimiter();

        return app;
    }

    public static WebApplication UseApiSwaggerDefaults(
        this WebApplication app,
        IConfiguration configuration,
        string apiDisplayName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!IsSwaggerEnabled(app.Environment, configuration))
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    url: $"/swagger/{description.GroupName}/swagger.json",
                    name: $"{apiDisplayName} {description.GroupName}{(description.IsDeprecated ? " (DEPRECATED)" : string.Empty)}");
            }

            options.RoutePrefix = "swagger";
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
