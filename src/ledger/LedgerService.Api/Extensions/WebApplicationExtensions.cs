using ApiDefaults.Extensions;

namespace LedgerService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        return app.UseApiSwaggerDefaults(configuration, "LedgerService API");
    }
}
