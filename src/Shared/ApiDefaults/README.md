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

builder.Services.AddApiDefaults<GlobalExceptionHandler>(builder.Configuration);

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
app.UseRateLimiter();

app.MapApiHealthEndpoints(
    static (_, _) => Task.FromResult(true),
    "Valida dependencias necessarias para aceitar trafego HTTP.");
```

## Forwarded Headers confiaveis

`AddApiDefaults` configura `X-Forwarded-For`, `X-Forwarded-Proto` e
`X-Forwarded-Host` com `ForwardLimit = 1`. Os hosts aceitos para
`X-Forwarded-Host` devem vir de `ForwardedHeaders:AllowedHosts`; o pacote
compartilhado nao injeta dominios locais nem produtivos por codigo.

Para Docker Compose local com Nginx e IP dinamico:

```json
{
  "ForwardedHeaders": {
    "AllowedHosts": [ "api.localhost", "localhost" ],
    "EnableLocalPermissiveMode": true
  }
}
```

Use esse modo somente em `Development` ou `Local`. Em GKE, Kubernetes, Cloud
Run, ingress ou load balancer, configure proxies ou redes confiaveis:

```json
{
  "ForwardedHeaders": {
    "TrustedProxies": [ "10.0.0.10" ],
    "TrustedNetworks": [ "10.128.0.0/20" ],
    "AllowedHosts": [ "api.example.com" ],
    "EnableLocalPermissiveMode": false
  }
}
```

Ambientes nao locais falham no startup quando nao informam pelo menos um proxy
ou CIDR confiavel e pelo menos um host encaminhado nao local. `localhost`,
subdominios `.localhost` e enderecos de loopback nao satisfazem a validacao de
ambientes produtivos.

## CORS configuravel

`ApiDefaults` registra CORS pela secao tipada `Cors`. O comportamento padrao e
fechado: se `Cors:Enabled=false` ou `Cors:AllowedOrigins` estiver vazio,
`UseApiDefaults` nao habilita CORS e a API nao emite
`Access-Control-Allow-Origin`.

Exemplo para uma API com consumidor browser:

```json
{
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": [ "https://app.example.com" ],
    "AllowedMethods": [ "GET", "POST" ],
    "AllowedHeaders": [
      "Authorization",
      "Content-Type",
      "Idempotency-Key",
      "X-Correlation-Id"
    ],
    "ExposedHeaders": [ "X-Correlation-Id" ],
    "AllowCredentials": false,
    "PreflightMaxAgeSeconds": 600
  }
}
```

Origens precisam ser absolutas, usar `http` ou `https` e nao podem conter path,
query string, fragmento ou wildcard. Nao use CORS como autenticacao ou
autorizacao de negocio; clientes server-to-server nao dependem do navegador e
nao sao protegidos por CORS.

Para Swagger/OpenAPI, use `AddApiSwaggerDefaults<TConfigureSwaggerOptions>` e `UseApiSwaggerDefaults` com uma implementacao de `IConfigureOptions<SwaggerGenOptions>` especifica da API.

## Rate limiting particionado

`AddApiDefaults` registra policies particionadas em memoria local da replica:

- `authenticated-read`;
- `authenticated-write`;
- `administrative`;
- `anonymous-webhook`;
- `fixed` como alias legado de escrita autenticada.

Use `UseRateLimiter()` depois de `UseAuthentication()` e `UseAuthorization()`
para que as policies autenticadas possam montar a particao com claims do token.
Health e readiness mapeados por `MapApiHealthEndpoints` continuam com
`DisableRateLimiting()`.

As chaves autenticadas seguem um modelo misto com predominancia de usuario final
quando `sub` ou `ClaimTypes.NameIdentifier` existe. A composicao e:

1. `sub` ou `ClaimTypes.NameIdentifier`;
2. `client_id` ou `azp`, como componente adicional quando presente;
3. `merchant_id` autorizado quando presente.

Quando nao houver identidade utilizavel, a chave usa fallback por IP remoto
normalizado, sem cair em uma particao global vazia. Os valores da chave sao
protegidos por hash e as metricas usam apenas labels de baixa cardinalidade.
Webhooks anonimos usam `RemoteIpAddress` apos `UseForwardedHeaders`; nao leia
`X-Forwarded-For` diretamente em aplicacoes.

Os defaults antigos continuam validos:

```json
{
  "ApiLimits": {
    "RateLimitPermitLimit": 100,
    "RateLimitWindowSeconds": 60,
    "RateLimitQueueLimit": 10
  }
}
```

Cada policy pode sobrescrever os limites:

```json
{
  "ApiLimits": {
    "AuthenticatedReadRateLimit": {
      "PermitLimit": 300,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "AuthenticatedWriteRateLimit": {
      "PermitLimit": 100,
      "WindowSeconds": 60,
      "QueueLimit": 10
    },
    "AdministrativeRateLimit": {
      "PermitLimit": 30,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "AnonymousWebhookRateLimit": {
      "PermitLimit": 120,
      "WindowSeconds": 60,
      "QueueLimit": 0
    }
  }
}
```

O modelo nao e distribuido: cada replica mantem seus contadores e janelas. Para
limite global por cliente, tenant ou merchant, avalie uma evolucao futura com
gateway/API management ou storage externo dedicado.

## Recursos

- Middlewares de correlation id, limite de body e headers de seguranca.
- Exception handling com `IExceptionHandler` e Problem Details.
- Swagger/OpenAPI com versionamento.
- Autenticacao JWT Bearer com JWKS.
- CORS configuravel por API e ambiente.
- Rate limiting particionado por janela fixa local a replica.
- Endpoints `/health` e `/ready`.
- OpenTelemetry para traces e metricas, incluindo metricas de resiliencia HTTP e
  rejeicoes de rate limiting com labels de baixa cardinalidade.

Esta e uma biblioteca de estudo/POC. Licenca MIT.
