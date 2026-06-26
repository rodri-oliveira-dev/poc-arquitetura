using ApiDefaults.Extensions;

using IdentityService.Api.Endpoints;
using IdentityService.Api.Extensions;
using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddIdentityApiComposition(builder.Configuration, builder.Environment);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwaggerDefaults(builder.Configuration, "IdentityService API");

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthEndpoints(
    static (services, cancellationToken) =>
        services.GetRequiredService<IdentityDbContext>().Database.CanConnectAsync(cancellationToken),
    "Valida dependencias necessarias para aceitar trafego HTTP: banco.");

app.MapUserEndpoints().RequireRateLimiting("fixed");

app.Run();
