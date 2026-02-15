# Observabilidade e rastreabilidade

## Objetivo

Definir como o projeto garante **rastreabilidade ponta a ponta** através de:

- **Traces distribuídos** (entrada HTTP, saída HTTP, banco quando aplicável)
- **Logs correlacionados**
- **Métricas** básicas
- **Propagação de contexto** para mensageria (Kafka)

> Este documento descreve o estado atual e o que foi implementado no repositório. Se algum item estiver como TODO, significa que não foi identificado/implementado no código ainda.

---

## Estado atual (comprovado no código)

### Correlação por `X-Correlation-Id`

**Evidência:** `src/LedgerService.Api/Middlewares/CorrelationIdMiddleware.cs`

- Header padrão: `X-Correlation-Id`
- Se o header não vier, ou vier inválido, a API **gera um novo UUID**.
- A API **propaga** o header:
  - no `HttpContext.Request.Headers`
  - no `HttpContext.Response.Headers`
- A API cria um **logging scope** contendo `CorrelationId`.

### Logs

**Evidência:**

- `src/LedgerService.Api/appsettings.json` -> `Logging:Console:IncludeScopes = true`
- `CorrelationIdMiddleware` e `OutboxKafkaPublisherService` usam `BeginScope(...)`

Campos de correlação presentes nos logs (por scope):

- `CorrelationId`

### Kafka (Outbox)

**Evidência:** `src/LedgerService.Infrastructure/Messaging/Kafka/OutboxKafkaProducer.cs`

Headers publicados atualmente:

- `event_id`
- `event_type`
- `correlation_id` (quando existir no `OutboxMessage`)

Propagação W3C (quando houver Activity atual):

- `traceparent`
- `tracestate` (opcional)
- `baggage` (opcional)

### Traces distribuídos e métricas

**Parcialmente implementado / TODO:**

- OpenTelemetry (tracing e métricas): implementado na API, porém **desabilitado por padrão** e dependente de configuração.
- Correlação automática de logs com `traceId/spanId`: **TODO** (não há Serilog/enricher configurado; depende do provider de logs utilizado).

---

## Padrões de correlação adotados

### 1) `X-Correlation-Id`

- Tipo: UUID (string)
- Origem: client (opcional) ou API (gerado)
- Propagação:
  - HTTP: request/response
  - Persistência: armazenado em `ledger_entries.correlation_id` e `outbox_messages.correlation_id`
  - Kafka: publicado em header `correlation_id`

### 2) `traceId/spanId` (OpenTelemetry)

- **TODO:** implementar instrumentação com OpenTelemetry e enriquecer logs com `traceId/spanId`.

### 3) W3C Trace Context para Kafka

- Implementado no publish do Kafka quando houver `Activity.Current`.
- **TODO:** como não há consumer Kafka no código, a extração desses headers no consume fica pendente.

---

## Como validar localmente

### Validar correlação HTTP

1. Faça uma chamada para um endpoint (ex.: `POST /api/v1/lancamentos`) com ou sem `X-Correlation-Id`.
2. Verifique que o response contém `X-Correlation-Id`.
3. Verifique nos logs que existe o scope `CorrelationId` associado.

### Validar outbox + Kafka

1. Configure Kafka e DB.
2. Crie um lançamento.
3. Confirme que uma linha é criada em `outbox_messages`.
4. Confirme que o publisher marcou como `Sent` após publicar.

**Dica:** os logs do publisher incluem escopo com `CorrelationId`, `OutboxId`, `EventType`, `AggregateId`.

### Validar traces/métricas

#### Habilitar OpenTelemetry via configuração

As opções estão em `Observability:OpenTelemetry`.

Exemplo (Windows PowerShell):

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
```

> Observação: o exporter de console é voltado para validação local. Para ambientes reais, recomenda-se configurar exporter OTLP e backend de tracing/métricas.
