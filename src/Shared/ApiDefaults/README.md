# PocArquitetura.ApiDefaults

Defaults compartilhados para APIs ASP.NET Core da POC `poc-arquitetura`.

Use este pacote quando uma API precisar reutilizar configuracoes comuns de borda HTTP, middlewares, Swagger/OpenAPI, versionamento, autenticacao JWT, CORS, rate limiting, health endpoints e OpenTelemetry.

Este pacote depende de `PocArquitetura.HttpResilienceDefaults` para configurar resiliencia no cliente HTTP usado na obtencao de JWKS.

## Instalacao

```bash
dotnet add package PocArquitetura.ApiDefaults
```

## Uso basico

```csharp
using ApiDefaults.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddApiDefaults<GlobalExceptionHandler>(
    builder.Configuration,
    "api.localhost",
    "localhost");

builder.Services.AddApiJwtBearerAuthentication(
    jwtOptions,
    builder.Environment,
    options => options.RequireAuthenticatedUserByDefault(),
    builder.Configuration);

builder.Services.AddApiOpenTelemetryDefaults(
    serviceName: "Example.Api",
    useConsoleExporter: builder.Environment.IsDevelopment(),
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"]);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthEndpoints(
    static (_, _) => Task.FromResult(true),
    "Valida dependencias necessarias para aceitar trafego HTTP.");
```

Para Swagger/OpenAPI, use `AddApiSwaggerDefaults<TConfigureSwaggerOptions>` e `UseApiSwaggerDefaults` com uma implementacao de `IConfigureOptions<SwaggerGenOptions>` especifica da API.

## Recursos

- Middlewares de correlation id, limite de body e headers de seguranca.
- Exception handling com `IExceptionHandler` e Problem Details.
- Swagger/OpenAPI com versionamento.
- Autenticacao JWT Bearer com JWKS.
- CORS padronizado.
- Rate limiting por janela fixa.
- Endpoints `/health` e `/ready`.
- OpenTelemetry para traces e metricas, incluindo metrica de resiliencia HTTP.

Esta e uma biblioteca de estudo/POC. Licenca MIT.
