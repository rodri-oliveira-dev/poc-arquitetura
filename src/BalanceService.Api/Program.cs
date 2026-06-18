using ApiDefaults.Extensions;

using BalanceService.Api.Extensions;
using BalanceService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddBalanceApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwagger(builder.Configuration);

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<BalanceDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program
{
}
