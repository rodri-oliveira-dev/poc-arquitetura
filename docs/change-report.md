# Change Report

Este documento registra as mudanças realizadas na tarefa de **Documentação técnica e rastreabilidade**.

## Resumo do que foi alterado

### Versionamento de API

- Configurado **Asp.Versioning** (URL segment) na API.
  - Formato: `api/v{version}/...`.
  - Versão padrão: `1.0`.
  - `ReportApiVersions=true` (exposição dos headers `api-supported-versions` / `api-deprecated-versions`).
- Ajustado controller existente (`LancamentosController`) para suportar versionamento via `[ApiVersion("1.0")]` e rota `api/v{version:apiVersion}/...`.

### Swagger/OpenAPI (multi-versões)

- Swagger agora gera **um documento por versão** via `IApiVersionDescriptionProvider`.
- SwaggerUI registra endpoints automaticamente para todas as versões disponíveis.

### Organização do Program

- Refatorado `Program.cs` para agrupar configurações em **métodos de extensão**:
  - `AddApiHardening`, `AddApiRateLimiting`, `AddApiCors`, `AddApiVersioningAndExplorer`, `AddApiSwagger`, `AddApiObservability`.
  - `UseApiSwagger`.

### Swagger/OpenAPI

- Habilitado XML comments no Swagger.
- Adicionada documentação do endpoint `POST /api/v1/lancamentos` (summary/description e respostas).
- Adicionado schema explícito para erro de validação (HTTP 400) via `ValidationErrorResponse`.
- Documentados contratos de request/response (`CreateLancamentoRequest`, `LancamentoDto`).

### README

- `README.md` reestruturado com seções exigidas (visão geral, arquitetura, pré-requisitos, execução local, testes, migrations, Kafka, observabilidade, troubleshooting, limitações).
- Removida exposição de segredo no README e introduzidos exemplos com `__REDACTED__`.

### Observabilidade e rastreabilidade

- Criado `docs/observability.md`.
- Implementada configuração opcional de OpenTelemetry na API (traces + métricas), desabilitada por padrão.
- Logs enriquecidos via scope com `CorrelationId` + `TraceId/SpanId` (quando houver Activity).
- Kafka: adicionada propagação de contexto W3C (`traceparent`, `tracestate`, `baggage`) no publish quando houver `Activity.Current`.
- Outbox publisher: criado `Activity` para operação de publish.

### Segurança de configuração

- `appsettings.json` atualizado para não conter senha em texto (placeholder `__REDACTED__`).

## Arquivos alterados / adicionados

### Adicionados

- `src/LedgerService.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/LedgerService.Api/Extensions/WebApplicationExtensions.cs`
- `src/LedgerService.Api/Swagger/ConfigureSwaggerOptions.cs`

- `docs/implementation-plan.md`
- `docs/observability.md`
- `docs/change-report.md`
- `src/LedgerService.Api/Contracts/ValidationErrorResponse.cs`
- `src/LedgerService.Api/Observability/OpenTelemetryOptions.cs`

### Alterados

- `src/LedgerService.Api/Program.cs`
- `src/LedgerService.Api/LedgerService.Api.csproj`
- `src/LedgerService.Api/Controllers/LancamentosController.cs`
- `src/LedgerService.Api/Controllers/Binds/CreateLancamentoBind.cs`
- `src/LedgerService.Api/LedgerService.Api.http`
- `src/LedgerService.Api/Contracts/CreateLancamentoRequest.cs`
- `src/LedgerService.Application/Common/Models/LancamentoDto.cs`
- `src/LedgerService.Api/Middlewares/GlobalExceptionHandler.cs`
- `src/LedgerService.Api/Middlewares/CorrelationIdMiddleware.cs`
- `src/LedgerService.Api/Controllers/Binds/CreateLancamentoBind.cs`
- `src/LedgerService.Infrastructure/Messaging/Kafka/OutboxKafkaProducer.cs`
- `src/LedgerService.Infrastructure/Outbox/OutboxKafkaPublisherService.cs`
- `src/LedgerService.Api/appsettings.json`
- `README.md`

## Evidências de validação

### Build

- `dotnet build .` ✅ (observação: warning NU1902 em `OpenTelemetry.Api 1.10.0`)

### Testes

- `dotnet test .` ✅ (8 testes aprovados)

### Swagger/OpenAPI

- API em execução e `GET /swagger/v1/swagger.json` retornando documento com:
  - `summary`/`description` do endpoint
  - schema `ValidationErrorResponse`
  - parâmetros de header `Idempotency-Key` e `X-Correlation-Id`

### Build

- `dotnet build src\\LedgerService.Api\\LedgerService.Api.csproj` ✅

## Pendências / TODOs

1. **NU1902 (OpenTelemetry.Api 1.10.0)**: há advisory de vulnerabilidade moderada.
   - TODO: avaliar atualização de versões do OpenTelemetry quando houver patch.
2. **Logs com traceId/spanId**:
   - Foi adicionado `TraceId/SpanId` ao scope no `CorrelationIdMiddleware`.
   - TODO: padronizar saída (ex.: Serilog + enrichers) se desejado.
3. **Consumers Kafka**:
   - Não identificados no código; portanto extração de `traceparent/baggage` no consume permanece pendente.
