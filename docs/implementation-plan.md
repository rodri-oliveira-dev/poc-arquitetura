# Plano de Implementação (Fase PLAN)

## Objetivo desta fase

Levantar o **estado atual comprovado no código/configuração** e propor um plano **incremental e seguro** para:

1. Melhorar documentação Swagger/OpenAPI (endpoints + DTOs).
2. Atualizar `README.md` com execução local, testes, migrations, Kafka e observabilidade.
3. Ajustar observabilidade para rastreabilidade ponta-a-ponta (traces + logs correlacionados + métricas), incluindo propagação de contexto para Kafka.

> Regra: **não inventar comportamento**. Onde não houver evidência, registrar como **“Não identificado no código”** e incluir **TODO**.

---

## 1) Estado atual encontrado (evidências)

### 1.1 Swagger/OpenAPI

**Evidência**

- Configuração Swashbuckle em `src/LedgerService.Api/Program.cs`:
  - `AddEndpointsApiExplorer()` + `AddSwaggerGen()` com 1 doc (`v1`).
  - `UseSwagger()` e `UseSwaggerUI()` habilitados com `RoutePrefix = string.Empty` (Swagger na raiz `/`).

**O que existe hoje**

- Um controller HTTP:
  - `POST /api/v1/lancamentos` em `src/LedgerService.Api/Controllers/LancamentosController.cs`.
- Metadados de resposta parcialmente definidos via attributes:
  - `201 Created` com `LancamentoDto`.
  - `400 BadRequest` (sem tipo explícito na attribute).
  - `409 Conflict` com `ProblemDetails`.

**O que não foi identificado no código**

- XML comments integrados ao Swagger (não há `IncludeXmlComments` nem `GenerateDocumentationFile` no `.csproj`).
- Anotações de `summary/description` por endpoint.
- Documentação de headers (`Idempotency-Key`, `X-Correlation-Id`) no contrato do OpenAPI (hoje só aparece como parâmetro por estar no action signature, porém sem descrição/semântica/exemplos).

### 1.2 Endpoints/contratos (comportamento observado)

#### `POST /api/v1/lancamentos`

**Evidência**

- Controller: `src/LedgerService.Api/Controllers/LancamentosController.cs`
- Request DTO: `src/LedgerService.Api/Contracts/CreateLancamentoRequest.cs`
- Bind/normalização/validação: `src/LedgerService.Api/Controllers/Binds/CreateLancamentoBind.cs`
- Service: `src/LedgerService.Application/Lancamentos/Services/CreateLancamentoService.cs`

**Headers exigidos/aceitos**

- `Idempotency-Key` (obrigatório; validado como GUID no `CreateLancamentoInputValidator`).
- `X-Correlation-Id` (opcional na action; o middleware garante um GUID e o bind resolve um valor sempre; validado como GUID no `CreateLancamentoInputValidator`).

**Regras importantes (comprovadas no código)**

- Rate limiting:
  - Limiter fixo em `Program.cs`: 100 req/min, fila 10, status `429`.
  - Controller exige `RequireRateLimiting("fixed")`.
- Validação (fail-fast) no bind via FluentValidation (`ValidateAndThrowAsync`).
- Normalização:
  - `Type` é normalizado para `UPPER` no bind.
  - `X-Correlation-Id`: se não vier no header, o bind usa o valor que o `CorrelationIdMiddleware` injetou no request.
- Idempotência:
  - Se `Idempotency-Key` já foi usada com payload diferente: retorna **409** (`ConflictException`).
  - Se existe registro e há `ResponseBody` armazenado: faz replay do `LancamentoDto`.
  - Se existe registro e não há `ResponseBody`: retorna **409** (`Unable to replay idempotent response.`).
- `Type`:
  - Só aceita `CREDIT` ou `DEBIT`.
- `Amount`:
  - Deve ser decimal em `InvariantCulture`.
  - Não pode ser `0`.
  - Regra por tipo:
    - `CREDIT`: `Amount > 0`
    - `DEBIT`: `Amount < 0`
  - Essas regras existem **na validação** (`CreateLancamentoInputValidator`) e **na entidade de domínio** (`LedgerEntry.EnsureValidAmount`).

**Resposta (comprovada no código)**

- `201` retorna `LancamentoDto`:
  - `Id` no formato `lan_{8 primeiros chars do Guid sem hífen}`.
  - `Type` retorna `CREDIT`/`DEBIT`.
  - `Amount` retorna string com 2 casas, `InvariantCulture`.
  - Datas em formato ISO-8601 (`"o"`).

### 1.3 Middlewares e erros

**Evidência**

- `src/LedgerService.Api/Middlewares/CorrelationIdMiddleware.cs`
- `src/LedgerService.Api/Middlewares/GlobalExceptionHandler.cs`
- `src/LedgerService.Api/Middlewares/SecurityHeadersMiddleware.cs`

**O que existe hoje**

- `CorrelationIdMiddleware`:
  - Garante `X-Correlation-Id` como GUID (gera se ausente/inválido).
  - Propaga no **request** e no **response**.
  - Cria `BeginScope` com `{ CorrelationId = <guid> }`.
- `GlobalExceptionHandler`:
  - Converte exceções para JSON.
  - Para `FluentValidation.ValidationException`: retorna body com `{ type, title, status, detail, errors, correlationId }` e status `400`.
  - Para demais: retorna `ProblemDetails` com `extensions["traceId"] = httpContext.TraceIdentifier`.
- `SecurityHeadersMiddleware`: adiciona headers básicos de segurança.

**O que não foi identificado no código**

- Autenticação/autorização (`AddAuthentication`, `[Authorize]`, etc.).

### 1.4 Persistência, migrations e banco

**Evidência**

- DbContext: `src/LedgerService.Infrastructure/Persistence/AppDbContext.cs`
- Migrations: `src/LedgerService.Infrastructure/Persistence/Migrations/*`
- Tooling: `dotnet-tools.json` contém `dotnet-ef`.
- Connection string em `src/LedgerService.Api/appsettings.json`.

**O que existe hoje**

- EF Core + PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- Migrations no projeto `LedgerService.Infrastructure`.
- Há constraint de integridade para valor de `amount` vs `type` na tabela `ledger_entries`.

**Atenção (segredo em repositório)**

- `appsettings.json` possui `ConnectionStrings:DefaultConnection` com `Username` e `Password` em texto.
  - A tarefa pede **não expor segredos no README**; além disso, idealmente o repositório também não deveria conter credenciais reais.
  - **TODO (ACT):** trocar por placeholder (ex.: `Password=__REDACTED__`) e documentar o uso via variável de ambiente/Secret Manager, mantendo um exemplo seguro.

### 1.5 Kafka + Outbox

**Evidência**

- DI/options:
  - `src/LedgerService.Infrastructure/DependencyInjection.cs`
  - `src/LedgerService.Infrastructure/Messaging/Kafka/KafkaProducerOptions.cs`
  - `src/LedgerService.Infrastructure/Outbox/OutboxPublisherOptions.cs`
- Producer:
  - `src/LedgerService.Infrastructure/Messaging/Kafka/OutboxKafkaProducer.cs`
- Publisher background:
  - `src/LedgerService.Infrastructure/Outbox/OutboxKafkaPublisherService.cs`
- Persistência Outbox:
  - `src/LedgerService.Domain/Entities/OutboxMessage.cs`
  - `src/LedgerService.Infrastructure/Repositories/OutboxMessageRepository.cs`

**O que existe hoje**

- Padrão Outbox:
  - A API grava `OutboxMessage` com `Status = Pending`.
  - Um `BackgroundService` (`OutboxKafkaPublisherService`) faz polling e publica.
  - Lock/claim de mensagens no PostgreSQL com `FOR UPDATE SKIP LOCKED` (evita concorrência).
  - Retentativas com backoff exponencial + jitter e limite de tentativas (`MaxAttempts`).
- Headers Kafka ao publicar:
  - `event_id`
  - `event_type`
  - `correlation_id` (quando existir)

**O que não foi identificado no código**

- Consumidores Kafka (não há `Consumer`/`IConsumer` no código analisado).
- Propagação de contexto de trace distribuído (ex.: `traceparent`) em mensagens Kafka.

### 1.6 Observabilidade (traces, logs, métricas)

**Evidência**

- Não foram encontrados usos de `OpenTelemetry`/`ActivitySource` no `src/`.
- Logging padrão do ASP.NET (`appsettings.json`), com `Console.IncludeScopes = true`.
- `CorrelationIdMiddleware` e outbox publisher usam `BeginScope`.

**Estado atual**

- Correlação por `X-Correlation-Id`:
  - Presente em request/response.
  - Propagado para Outbox e publicado em header Kafka (`correlation_id`).
- `traceId`/`spanId`:
  - Em erros via `ProblemDetails.Extensions["traceId"] = httpContext.TraceIdentifier`.
  - **Não identificado no código** enriquecimento sistemático de logs com `traceId/spanId` de tracing distribuído (Activity).
- Métricas:
  - **Não identificado no código** emissão de métricas de latência/erro por endpoint.

---

## 2) Lacunas identificadas

### Swagger/OpenAPI

- Falta `summary/description` por endpoint.
- Falta documentação de semântica/obrigatoriedade/exemplos dos campos dos DTOs.
- Falta documentação de headers (`Idempotency-Key`, `X-Correlation-Id`) explicando regras e exemplos.
- Falta especificação clara das respostas de erro:
  - `400` por validação retorna um JSON que **não é** `ProblemDetails` (formato custom), mas isso não está documentado no OpenAPI.
  - `409` usa `ProblemDetails` (mapeado por exception handler) mas sem explicação.

### README

- README atual cobre bem Outbox/Kafka e migrations, mas falta:
  - propósito/visão geral e arquitetura;
  - pré-requisitos (SDK .NET, PostgreSQL, Kafka, etc.);
  - como executar localmente (passo a passo completo);
  - como rodar testes;
  - seção de observabilidade/rastreabilidade;
  - troubleshooting e limitações conhecidas.

### Observabilidade ponta a ponta

- Não há tracing distribuído (OpenTelemetry) no projeto.
- Logs têm `CorrelationId` via scopes, mas:
  - não há padronização explícita de campos `traceId/spanId` nos logs;
  - não há propagação de contexto `traceparent`.
- Kafka:
  - há `correlation_id`, mas não há headers W3C (`traceparent`, `tracestate`, `baggage`).

### Contratos

- (Atualização durante ACT) `CreateLancamentoRequest` **não possui** `OccurredAt` no código atual.
  - **TODO:** caso a API precise suportar o campo no request, decidir a abordagem e documentar o impacto de compatibilidade.

---

## 3) Lista de arquivos candidatos a alteração (futuro ACT)

> Observação: lista baseada no estado atual. Pode ajustar após rodar build/test e validar eventuais inconsistências.

### Swagger/OpenAPI

- `src/LedgerService.Api/Program.cs` (config Swagger: XML comments, filtros de schema/operação, responses).
- `src/LedgerService.Api/Controllers/LancamentosController.cs` (summary/description/responses).
- `src/LedgerService.Api/Contracts/CreateLancamentoRequest.cs` (docs de campos).
- `src/LedgerService.Application/Common/Models/LancamentoDto.cs` (docs de campos/semântica).

### Observabilidade

- `src/LedgerService.Api/Program.cs` (OpenTelemetry tracing+metrics; config por env).
- `src/LedgerService.Api/Middlewares/CorrelationIdMiddleware.cs` (padronizar escopos/campos; correlação com traceId).
- `src/LedgerService.Api/Middlewares/GlobalExceptionHandler.cs` (incluir correlationId + traceId em todos os erros de forma consistente e documentada).
- `src/LedgerService.Infrastructure/Messaging/Kafka/OutboxKafkaProducer.cs` (propagar trace headers; logs com trace/correlation).
- `src/LedgerService.Infrastructure/Outbox/OutboxKafkaPublisherService.cs` (criar Activity por publish; scope/logs).

### README + Docs

- `README.md` (reestrutura completa conforme checklist da tarefa).
- `docs/observability.md` (novo).
- `docs/change-report.md` (novo, ao final do ACT).

### Configuração segura

- `src/LedgerService.Api/appsettings.json` (substituir segredos por placeholders; reforçar env vars).
- `src/LedgerService.Api/appsettings.Development.json` (se necessário para exemplos locais sem segredos).

---

## 4) Plano de implementação por etapas (futuro ACT)

### Etapa 0 — Baseline de validação

1. Rodar `dotnet build` e `dotnet test`.
2. Subir API e verificar geração do Swagger (`/swagger/v1/swagger.json`).
3. Registrar resultado no `docs/change-report.md`.

> Se o build falhar por inconsistências (ex.: contrato vs bind), abrir TODO e corrigir antes de avançar.

### Etapa 1 — Swagger/OpenAPI (documentação sem alterar regra de negócio)

1. Habilitar XML Comments no(s) `.csproj` relevante(s) e integrar ao Swagger via `IncludeXmlComments`.
2. Adicionar `summary/description` aos endpoints (Controller/Action).
3. Documentar:
   - headers `Idempotency-Key` e `X-Correlation-Id` (semântica, obrigatoriedade, formato GUID, exemplos);
   - responses: 201/400/409 (+ 429 por rate limiting; e 500/422/404 conforme handler).
4. DTOs:
   - Documentar semântica dos campos (MerchantId, Type, Amount, Description, ExternalReference, timestamps).
   - Adicionar exemplos quando fizer sentido.
5. Padronizar documentação do erro 400 de validação (formato custom) no OpenAPI.

### Etapa 2 — README.md

Reescrever `README.md` com as seções exigidas:

1. Visão geral do projeto
2. Arquitetura e principais componentes
3. Pré-requisitos
4. Como executar localmente
5. Como rodar testes
6. Banco de dados e migrations
7. Kafka
8. Observabilidade e rastreabilidade
9. Troubleshooting básico
10. Limitações conhecidas

Regras:

- Remover/evitar segredos (usar placeholders e instruir via env var).
- Referenciar caminhos reais de config (`appsettings*.json`, options, etc.).

### Etapa 3 — Observabilidade (tracing + logs correlacionados + métricas)

1. Adicionar OpenTelemetry (tracing e métricas) com configuração por variável de ambiente.
2. Tracing:
   - entrada HTTP (`AspNetCore` instrumentation);
   - saída HTTP (`HttpClient` instrumentation) **se/quando houver**;
   - banco (EF Core) **se suportado**.
3. Logs correlacionados:
   - garantir presença de `CorrelationId` + `TraceId`/`SpanId` (quando houver Activity) em todos os logs relevantes.
4. Métricas básicas:
   - latência e taxa de erro por endpoint (via instrumentation).

### Etapa 4 — Kafka: propagação de contexto

1. Ao publicar no Kafka (Outbox):
   - manter headers existentes (`event_id`, `event_type`, `correlation_id`);
   - adicionar headers W3C quando houver Activity atual: `traceparent`, `tracestate` e `baggage`.
2. Criar `Activity` para o publish no `OutboxKafkaPublisherService` quando necessário, para rastrear o trabalho do background.

> Consumidores Kafka não foram identificados no código; portanto, a parte de **extração** desses headers no consume ficará como TODO caso consumidores sejam adicionados futuramente.

### Etapa 5 — Documentação de observabilidade

Criar `docs/observability.md` com:

- Arquitetura de telemetria;
- campos de correlação adotados (`X-Correlation-Id`, `traceId/spanId`, `traceparent`);
- como validar localmente (o que observar em logs, exemplos de headers e payloads).

### Etapa 6 — Validação final e relatório

1. Executar:
   - `dotnet build`
   - `dotnet test`
   - geração/checagem do Swagger JSON
2. Criar `docs/change-report.md` com:
   - resumo do que foi alterado;
   - arquivos alterados;
   - evidências de validação;
   - pendências/TODOs.

---

## 5) Riscos e mitigação

1. **Risco:** adicionar OpenTelemetry pode introduzir overhead.
   - **Mitigação:** habilitar/exportar via configuração; defaults seguros (ex.: sem exporter remoto se não configurado).
2. **Risco:** mudança no contrato `OccurredAt` pode quebrar clientes.
   - **Mitigação:** manter compatibilidade; documentar claramente a situação; somente alterar contrato após decisão explícita.
3. **Risco:** incluir headers adicionais no Kafka pode impactar consumidores antigos.
   - **Mitigação:** adicionar headers de forma não-breaking (apenas novos headers), sem alterar payload.
4. **Risco:** logs mais “verbosos”.
   - **Mitigação:** ajustar níveis e evitar logar payloads/sensíveis.

---

## 6) Critérios de aceite (para a fase ACT)

1. Swagger/OpenAPI:
   - endpoints com `summary/description`;
   - DTOs e headers documentados com semântica/obrigatoriedade;
   - respostas de erro (400/409/429/500 etc.) documentadas.
2. README:
   - contém todas as seções exigidas e instruções de execução/testes/migrations/Kafka;
   - sem segredos expostos.
3. Observabilidade:
   - traces habilitáveis por configuração;
   - logs contendo `CorrelationId` e (quando aplicável) `TraceId/SpanId`;
   - propagação de headers de tracing no publish Kafka (`traceparent` etc.) quando houver Activity;
   - `docs/observability.md` criado com instruções de validação.
4. Validação:
   - `dotnet build` e `dotnet test` executam com sucesso (ou resultado registrado com causa e TODO).
