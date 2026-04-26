using Confluent.Kafka;

using LedgerService.Api.Extensions;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Options;
using LedgerService.Application;
using LedgerService.Infrastructure;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;

    var maxRequestBodySizeBytes = context.Configuration.GetValue<long?>(
        $"{ApiLimitsOptions.SectionName}:{nameof(ApiLimitsOptions.MaxRequestBodySizeBytes)}");

    if (maxRequestBodySizeBytes is > 0)
        options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes;
});

builder.Services
    .AddApiHardening(builder.Configuration)
    .AddApiRateLimiting(builder.Configuration)
    .AddApiCors()
    .AddApiVersioningAndExplorer()
    .AddApiSwagger()
    .AddApiObservability(builder.Configuration);

builder.Services.AddApiJwtAuth(builder.Configuration, builder.Environment);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseApiSwagger(builder.Configuration);

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
app.UseMiddleware<RequestBodySizeLimitMiddleware>();
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
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var checks = new Dictionary<string, string>
    {
        ["db"] = await db.Database.CanConnectAsync(cancellationToken) ? "ok" : "unavailable"
    };

    var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
    checks["kafka"] = kafkaEnabled ? CheckKafkaProducerReadiness(configuration) : "disabled";

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

static string CheckKafkaProducerReadiness(IConfiguration configuration)
{
    var options = configuration.GetSection(KafkaProducerOptions.SectionName).Get<KafkaProducerOptions>()
        ?? new KafkaProducerOptions();

    if (string.IsNullOrWhiteSpace(options.BootstrapServers))
        return "unconfigured";

    try
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = $"{options.ClientId}-readiness"
        };
        adminConfig.ApplySecurity(options);

        using var admin = new AdminClientBuilder(adminConfig).Build();

        var topics = options.TopicMap.Values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (topics.Count == 0)
            topics.Add(options.DefaultTopic);

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
