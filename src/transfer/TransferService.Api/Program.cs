using ApiDefaults.Extensions;

using TransferService.Api.Extensions;
using TransferService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddTransferApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwagger(builder.Configuration);

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<TransferServiceDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program
{
}
