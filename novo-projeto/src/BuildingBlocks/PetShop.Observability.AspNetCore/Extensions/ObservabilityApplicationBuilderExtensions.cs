using Microsoft.AspNetCore.Builder;

using PetShop.Observability.AspNetCore.Middlewares;

namespace PetShop.Observability.AspNetCore.Extensions;

public static class ObservabilityApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePetShopObservabilityContext(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<CorrelationContextMiddleware>();
    }
}
