using ApiDefaults.Extensions;

using LedgerService.Api.Extensions;
using LedgerService.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddLedgerApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwagger(builder.Configuration);

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", [AllowAnonymous] () => Results.Text("ok"))
    .WithGroupName("v1")
    .WithName("Health")
    .WithSummary("Health check simples")
    .WithDescription("Retorna 200 com body 'ok'. Endpoint público para liveness simples.")
    .Produces(StatusCodes.Status200OK, contentType: "text/plain")
    .DisableRateLimiting();

app.MapGet("/ready", [AllowAnonymous] async (
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var checks = new Dictionary<string, string>
    {
        ["db"] = await db.Database.CanConnectAsync(cancellationToken) ? "ok" : "unavailable"
    };

    var ready = checks.Values.All(v => v is "ok");
    return ready
        ? Results.Ok(new
        {
            status = "ready",
            checks
        })
        : Results.Json(new
        {
            status = "not_ready",
            checks
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .WithGroupName("v1")
    .WithName("Ready")
    .WithSummary("Readiness check")
    .WithDescription("Valida dependências necessárias para aceitar tráfego HTTP: banco.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable)
    .DisableRateLimiting();

app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program
{
}
