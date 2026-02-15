# poc-arquitetura (LedgerService)

## 1. Visão geral do projeto

Este repositório é uma POC de **Clean Architecture** com foco em **DDD**, demonstrando:

- **API HTTP** para criação de lançamentos (`lancamentos`).
- Persistência com **Entity Framework Core + PostgreSQL**.
- Publicação de eventos via **Kafka** usando o padrão **Outbox** (entrega *at-least-once*).
- Base para **rastreabilidade** via *correlation id* (e, na fase de observabilidade, tracing distribuído).

> Importante: o endpoint **não publica diretamente no Kafka**. Ele grava o evento em `outbox_messages` (status `Pending`) e um `BackgroundService` publica em background.

## 2. Arquitetura e principais componentes

- `src/LedgerService.Api`:
  - ASP.NET Core Controllers + Swagger
  - Middlewares: `CorrelationIdMiddleware`, `GlobalExceptionHandler`, `SecurityHeadersMiddleware`
- `src/LedgerService.Application`:
  - Casos de uso/Services (ex.: `CreateLancamentoService`)
  - FluentValidation (validators de inputs)
- `src/LedgerService.Domain`:
  - Entidades e regras de domínio (`LedgerEntry`, `OutboxMessage`, etc.)
- `src/LedgerService.Infrastructure`:
  - EF Core (`AppDbContext`) + migrations
  - Outbox publisher e integração Kafka (`OutboxKafkaPublisherService`, `OutboxKafkaProducer`)
- `tests/LedgerService.Tests`:
  - Testes unitários e de repositório

## 3. Pré-requisitos

- .NET SDK (recomendado: **.NET 10**)
- PostgreSQL acessível localmente
- Kafka acessível localmente (caso queira validar publicação)

## 4. Como executar localmente (passo a passo)

### 4.1 Restaurar tools locais

O repositório versiona o `dotnet-ef` via `dotnet-tools.json`.

```bash
dotnet tool restore
```

### 4.2 Configurar variáveis de ambiente / appsettings

Configurações ficam em:

- `src/LedgerService.Api/appsettings.json`
- `src/LedgerService.Api/appsettings.Development.json`

**Não coloque segredos no repositório.** Para execução local, use variáveis de ambiente.

Exemplos (Windows PowerShell):

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=__REDACTED__"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
```

> Observação: o formato `__` representa o separador de seções do .NET Configuration.

### 4.3 Aplicar migrations

As migrations ficam no projeto `LedgerService.Infrastructure` (onde está o `AppDbContext`).

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

> O `-- --environment Development` é repassado para a aplicação (startup project) para ela carregar `appsettings.Development.json`.

### 4.4 Subir a API

```bash
dotnet run --project src\\LedgerService.Api\\LedgerService.Api.csproj
```

- Swagger UI: `http://localhost:5226/`
- OpenAPI JSON: `http://localhost:5226/swagger/v1/swagger.json`

## 5. Como rodar testes

```bash
dotnet test
```

## 6. Banco de dados e migrations

### 6.1 Listar migrations

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext
```

### 6.2 Criar nova migration

```bash
dotnet tool run dotnet-ef -- migrations add NomeDaMigration \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -o Persistence\\Migrations
```

### 6.3 Aplicar migration

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

### 6.4 Reverter migration (quando aplicável)

O EF Core permite voltar para uma migration específica (inclusive `0`).

```bash
dotnet tool run dotnet-ef -- database update NomeDaMigrationAnteriorOu0 \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

## 7. Kafka (se aplicável)

### 7.1 Onde ficam as configurações

- Producer: `Kafka:Producer` (`src/LedgerService.Api/appsettings.json`)
- Outbox publisher: `Outbox:Publisher` (`src/LedgerService.Api/appsettings.json`)

**Exemplo (sem segredos):**

```json
{
  "Kafka": {
    "Producer": {
      "BootstrapServers": "127.0.0.1:9092",
      "ClientId": "ledger-service",
      "Acks": "all",
      "EnableIdempotence": true,
      "DefaultTopic": "ledger-events",
      "TopicMap": {
        "LedgerEntryCreated": "ledger.ledgerentry.created"
      }
    }
  },
  "Outbox": {
    "Publisher": {
      "PollingIntervalSeconds": 5,
      "BatchSize": 50,
      "MaxParallelism": 4,
      "MaxAttempts": 10,
      "BaseBackoffSeconds": 5,
      "LockDurationSeconds": 60
    }
  }
}
```

### 7.2 Tópicos publicados

- Evento: `LedgerEntryCreated`
- Tópico (por padrão): `ledger-events`
- Mapeamento atual em `TopicMap`: `LedgerEntryCreated` -> `ledger.ledgerentry.created`

### 7.3 Headers publicados

Ao publicar, o producer inclui headers:

- `event_id`
- `event_type`
- `correlation_id` (quando existir)

> Observação: a propagação de headers W3C (`traceparent`, `baggage`) depende da configuração de observabilidade (ver seção 8 e `docs/observability.md`).

### 7.4 Como validar (PENDING -> SENT)

1. Aplicar migrations no PostgreSQL.
2. Subir a API.
3. Criar um lançamento via `POST /api/v1/lancamentos`.
4. Verificar no banco:
   - ao criar, surge uma linha em `outbox_messages` com `status = 'Pending'`
   - após alguns segundos, o publisher marca como `status = 'Sent'` (após confirmação do publish no Kafka)

Em caso de falha no Kafka, o serviço não cai: ele registra erro, incrementa tentativas e agenda `next_attempt_at` com backoff.

## 8. Observabilidade e rastreabilidade

### 8.1 Correlação (estado atual)

- A API usa o header `X-Correlation-Id`:
  - se ausente/inválido, gera um novo UUID;
  - retorna o mesmo header no response;
  - adiciona `CorrelationId` nos logs via logging scope.

### 8.2 Traces e métricas

Consulte `docs/observability.md` para:

- arquitetura de telemetria;
- campos de correlação adotados (`CorrelationId`, `traceId/spanId`);
- como validar localmente.

> TODO: documento será criado/atualizado na etapa de observabilidade.

## 9. Troubleshooting básico

- **Erro ao aplicar migrations**: confirme a connection string e se o PostgreSQL está acessível.
- **Swagger não abre**: confirme que a aplicação está rodando e acessível na URL configurada (`launchSettings.json`).
- **Outbox publisher logando queries repetidas**: comportamento esperado (polling). Ajuste `Outbox:Publisher:PollingIntervalSeconds`.

## 10. Limitações conhecidas

- Não há autenticação/autorização configurada (não identificado no código).
- Consumidores Kafka não estão implementados (apenas producer/outbox publisher).