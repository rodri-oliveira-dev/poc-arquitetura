using BalanceService.Api.Extensions;
using BalanceService.Api.Middlewares;
using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Infrastructure.Messaging.Kafka;
using BalanceService.Infrastructure.Persistence;

using Confluent.Kafka;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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
if (!app.Environment.IsEnvironment("Test"))
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("ApiCorsPolicy");
app.UseRateLimiter();
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
    BalanceDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var checks = new Dictionary<string, string>
    {
        ["db"] = await db.Database.CanConnectAsync(cancellationToken) ? "ok" : "unavailable"
    };

    var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
    checks["kafka"] = kafkaEnabled ? CheckKafkaConsumerReadiness(configuration) : "disabled";

    var ready = checks.Values.All(v => v is "ok" or "disabled");
    return ready
        ? Results.Ok(new { status = "ready", checks })
        : Results.Json(new { status = "not_ready", checks }, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .WithGroupName("v1")
    .WithName("Ready")
    .WithSummary("Readiness check")
    .WithDescription("Valida dependências necessárias para aceitar tráfego: banco e Kafka quando habilitado.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable)
    .DisableRateLimiting();

app.MapControllers().RequireRateLimiting("fixed");

app.Run();

static string CheckKafkaConsumerReadiness(IConfiguration configuration)
{
    var options = configuration.GetSection(KafkaConsumerOptions.SectionName).Get<KafkaConsumerOptions>()
        ?? new KafkaConsumerOptions();

    if (string.IsNullOrWhiteSpace(options.BootstrapServers) || options.Topics.Count == 0)
        return "unconfigured";

    try
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = $"{options.ClientId}-readiness"
        }).Build();

        var topics = options.Topics
            .Append(options.DeadLetterTopic)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal);

        foreach (var topic in topics)
            admin.GetMetadata(topic, TimeSpan.FromSeconds(3));

        return "ok";
    }
    catch
    {
        return "unavailable";
    }
}

public partial class Program { }
