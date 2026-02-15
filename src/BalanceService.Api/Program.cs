using BalanceService.Api.Extensions;
using BalanceService.Api.Middlewares;
using BalanceService.Application;
using BalanceService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => { options.AddServerHeader = false; });

builder.Services
    .AddApiHardening()
    .AddApiRateLimiting()
    .AddApiCors()
    .AddApiVersioningAndExplorer()
    .AddApiSwagger()
    .AddApiObservability(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

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
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");

app.Run();
