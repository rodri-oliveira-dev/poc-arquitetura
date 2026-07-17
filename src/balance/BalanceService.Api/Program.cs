using ApiDefaults.Extensions;

using BalanceService.Api.Extensions;
using BalanceService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddBalanceApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiDefaults();
app.UseApiSwagger(builder.Configuration);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<BalanceDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapControllers();

app.Run();

public partial class Program
{
}
