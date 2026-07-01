using ApiDefaults.Extensions;

using LedgerService.Api.Extensions;
using LedgerService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddLedgerApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwagger(builder.Configuration);

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<AppDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program
{
}
