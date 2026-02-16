using LedgerService.Api.Extensions;
using LedgerService.Api.Middlewares;
using LedgerService.Application;
using LedgerService.Infrastructure;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => { options.AddServerHeader = false; });

builder.Services
    .AddApiHardening()
    .AddApiRateLimiting()
    .AddApiCors()
    .AddApiVersioningAndExplorer()
    .AddApiSwagger()
    .AddApiObservability(builder.Configuration);

builder.Services.AddApiJwtAuth(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseApiSwagger();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("ApiCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health check simples (público)
// - Não depende de DB/Kafka (liveness básico)
// - Mantém consistência com Auth.Api
app.MapGet("/health", [AllowAnonymous] () => Results.Text("ok"))
    .WithGroupName("v1")
    .WithName("Health")
    .WithSummary("Health check simples")
    .WithDescription("Retorna 200 com body 'ok'. Endpoint público para liveness/readiness simplificado.")
    .Produces(StatusCodes.Status200OK, contentType: "text/plain")
    .DisableRateLimiting();

app.MapControllers().RequireRateLimiting("fixed");

app.Run();
