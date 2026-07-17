using ApiDefaults.Extensions;

using AuditService.Api.Extensions;
using AuditService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddAuditApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiDefaults();
app.UseApiSwagger(builder.Configuration);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<AuditDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapControllers();

app.Run();
