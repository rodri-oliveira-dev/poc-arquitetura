using ApiDefaults.Extensions;

namespace BalanceService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        return app.UseApiSwaggerDefaults(configuration, "BalanceService API");
    }
}
