using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace ApiDefaults.Extensions;

public static class HealthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiHealthEndpoints(
        this IEndpointRouteBuilder app,
        Func<IServiceProvider, CancellationToken, Task<bool>> canConnectToDatabase,
        string readinessDescription)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(canConnectToDatabase);
        ArgumentException.ThrowIfNullOrWhiteSpace(readinessDescription);

        app.MapGet("/health", [AllowAnonymous] () => Results.Text("ok"))
            .WithGroupName("v1")
            .WithName("Health")
            .WithTags("Operacional")
            .WithSummary("Health check simples")
            .WithDescription("Retorna 200 com body 'ok'. Endpoint publico para liveness simples.")
            .Produces(StatusCodes.Status200OK, contentType: "text/plain")
            .DisableRateLimiting();

        app.MapGet("/ready", [AllowAnonymous] async (
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            Dictionary<string, string> checks = new()
            {
                ["db"] = await canConnectToDatabase(httpContext.RequestServices, cancellationToken) ? "ok" : "unavailable"
            };

            if (checks.Values.All(static value => value is "ok"))
            {
                return Results.Ok(new
                {
                    status = "ready",
                    checks
                });
            }

            return Results.Json(
                new
                {
                    status = "not_ready",
                    checks
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        })
            .WithGroupName("v1")
            .WithName("Ready")
            .WithTags("Operacional")
            .WithSummary("Readiness check")
            .WithDescription(readinessDescription)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .DisableRateLimiting();

        return app;
    }
}
