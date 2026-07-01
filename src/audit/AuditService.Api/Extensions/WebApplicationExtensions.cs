using ApiDefaults.Extensions;

namespace AuditService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        return app.UseApiSwaggerDefaults(configuration, "AuditService API");
    }
}
