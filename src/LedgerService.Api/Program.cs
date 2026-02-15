using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Observability;
using LedgerService.Application;
using LedgerService.Infrastructure;
using System.Threading.RateLimiting;
using System.Reflection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => { options.AddServerHeader = false; });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 10;
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// Observabilidade (OpenTelemetry)
// - Por padrão fica desabilitado.
// - Pode ser habilitado via config: Observability:OpenTelemetry:Enabled=true
// - Para validar localmente sem backend, use Observability:OpenTelemetry:UseConsoleExporter=true
builder.Services.AddOptions<OpenTelemetryOptions>()
    .Bind(builder.Configuration.GetSection(OpenTelemetryOptions.SectionName));

var otelOptions = builder.Configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
    ?? new OpenTelemetryOptions();

if (otelOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (otelOptions.UseConsoleExporter)
                tracing.AddConsoleExporter();
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (otelOptions.UseConsoleExporter)
                metrics.AddConsoleExporter();
        });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCorsPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://localhost:3001",
                "https://localhost:5173")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Poc Arquitetura API",
        Version = "v1",
        Description = "API em Clean Architecture com foco em DDD"
    });

    // XML comments para enriquecer Swagger/OpenAPI com summary/remarks de controllers e DTOs.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Headers comuns da API (correlação e idempotência)
    options.AddSecurityDefinition("Idempotency-Key", new OpenApiSecurityScheme
    {
        Name = "Idempotency-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Chave de idempotência (UUID). Requisições com a mesma chave e mesmo payload podem ser reprocessadas com replay da resposta. Se a mesma chave for usada com payload diferente, a API retorna 409."
    });

    options.AddSecurityDefinition(CorrelationIdMiddleware.HeaderName, new OpenApiSecurityScheme
    {
        Name = CorrelationIdMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Identificador de correlação (UUID). Se não for enviado, a API gera um novo e o retorna no header de response."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LedgerService API v1");
    options.RoutePrefix = string.Empty;
});

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
