using ApiDefaults.Extensions;

namespace PaymentService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiSwagger(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        return app.UseApiSwaggerDefaults(configuration, "PaymentService API");
    }
}
