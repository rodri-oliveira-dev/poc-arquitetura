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

        return app.MapApiHealthEndpoints(
            static async (services, state, cancellationToken) => new Dictionary<string, string>
            {
                ["db"] = await state(services, cancellationToken) ? "ok" : "unavailable"
            },
            canConnectToDatabase,
            readinessDescription);
    }

    public static IEndpointRouteBuilder MapApiHealthEndpoints<TState>(
        this IEndpointRouteBuilder app,
        Func<IServiceProvider, TState, CancellationToken, Task<IReadOnlyDictionary<string, string>>> readinessChecks,
        TState state,
        string readinessDescription)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(readinessChecks);
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
            IReadOnlyDictionary<string, string> checks = await readinessChecks(httpContext.RequestServices, state, cancellationToken);

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
