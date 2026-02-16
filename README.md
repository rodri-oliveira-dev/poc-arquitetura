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

### BalanceService (consolidado)

- `src/BalanceService.Api`:
  - API HTTP (somente leitura do consolidado)
  - Swagger multi-versão
  - Middlewares: `CorrelationIdMiddleware`, `GlobalExceptionHandler`, `SecurityHeadersMiddleware`
- `src/BalanceService.Application`:
  - Handlers de queries para consulta do consolidado (diário e por período)
- `src/BalanceService.Infrastructure`:
  - EF Core (`BalanceDbContext`) + migrations
  - Consumer Kafka (`LedgerEventsConsumer`) que alimenta a tabela `daily_balances`

## 3. Pré-requisitos

- .NET SDK (recomendado: **.NET 10**)
- PostgreSQL acessível localmente
- Kafka acessível localmente (caso queira validar publicação)

## 3.1 VS Code (workspace + extensões recomendadas)

Este repositório inclui configuração para facilitar a execução no **VS Code** (com .NET 10, HTTP client e Mermaid no Markdown).

### Arquivos adicionados

- `poc-arquitetura.code-workspace`
  - define a solução padrão (`LedgerService.slnx`)
  - ajusta excludes (`bin/`, `obj/`, `.vs/`)
  - habilita Mermaid no preview de Markdown
- `.vscode/extensions.json`
  - recomenda extensões para C#/.NET, REST Client, Docker/K8s, YAML e Markdown/Mermaid
- `.vscode/settings.json`
  - configurações locais do workspace (format on save, excludes, Mermaid)
- `.vscode/launch.json` + `.vscode/tasks.json`
  - perfis de execução/debug para `LedgerService.Api` e `BalanceService.Api`
- `.vscode/rest-client.env.json`
  - variáveis por ambiente para o **REST Client** (sem segredos)

### Como abrir

No VS Code:

1. **File > Open Workspace from File...**
2. Selecione `poc-arquitetura.code-workspace`
3. Instale as extensões sugeridas quando o VS Code perguntar.

### Como chamar a API pelo VS Code

O projeto já tem um arquivo `src/LedgerService.Api/LedgerService.Api.http` (REST Client).

- Abra o arquivo `.http` e clique em **Send Request**.
- Para alternar ambientes, use a seleção de environment do REST Client.
- Variáveis ficam em `.vscode/rest-client.env.json` (ex.: `ledgerBaseUrl`, `balanceBaseUrl`).

> Importante: **não versionar segredos** nesse arquivo. Use somente URLs e valores de exemplo.

## 4. Como executar localmente (passo a passo)

### 4.0 Subir dependências e microserviços via nerdctl compose

Este repositório inclui um `compose.yaml` preparado para **nerdctl compose** (containerd), com:

- 2 bancos **PostgreSQL** (um por microserviço)
- 1 **Kafka** (single node / KRaft)
- 1 job de init para criar tópico(s) necessários
- 2 microserviços (**LedgerService.Api** e **BalanceService.Api**) na **mesma rede**

> Importante: no `BalanceService`, o consumer está com `AllowAutoCreateTopics=false`. Por isso o compose cria o tópico `ledger.ledgerentry.created` no startup.

#### Subir stack

```bash
nerdctl compose up -d --build
```

#### Parar stack

```bash
nerdctl compose down
```

#### Portas expostas (host)

- LedgerService.Api: `http://localhost:5226/`
- BalanceService.Api: `http://localhost:5228/`
- PostgreSQL Ledger: `localhost:15432` (container: `ledger-db:5432`)
- PostgreSQL Balance: `localhost:15433` (container: `balance-db:5432`)
- Kafka: `localhost:19092` (container: `kafka:9092`)

#### Observação sobre appsettings em container

Os `appsettings.json` usam `127.0.0.1` por padrão (para execução fora de container). No compose eu faço override por variáveis de ambiente:

- `ConnectionStrings__DefaultConnection`
- `Kafka__Producer__BootstrapServers`
- `Kafka__Consumer__BootstrapServers`

Assim, **dentro da rede do compose** os serviços usam `ledger-db`, `balance-db` e `kafka` como hosts.

#### Migrations (quando rodando via compose)

O compose **não aplica migrations automaticamente** (para evitar comportamento implícito em infraestrutura).

Você pode aplicar migrations a partir do host usando as portas expostas dos Postgres:

**LedgerService (AppDbContext):**

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=app123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

**BalanceService (BalanceDbContext):**

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=Balance123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

> TODO: se quiser automatizar migrations no startup do compose, criar um job/sidecar explícito para isso.

#### Como validar rapidamente

```bash
# Ver status dos containers
nerdctl compose ps

# Ver logs (exemplo)
nerdctl compose logs -f ledger-service
```

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
- OpenAPI JSON:
  - `http://localhost:5226/swagger/v1/swagger.json`

### 4.4.1 Subir o BalanceService.Api

```bash
dotnet run --project src\\BalanceService.Api\\BalanceService.Api.csproj
```

- Swagger UI: `http://localhost:5227/`
- OpenAPI JSON:
  - `http://localhost:5227/swagger/v1/swagger.json`

#### Rotas de consulta (BalanceService)

- `GET /v1/consolidados/diario/{date}?merchantId={merchantId}`
- `GET /v1/consolidados/periodo?from=YYYY-MM-DD&to=YYYY-MM-DD&merchantId={merchantId}`

> Observação: padrão adotado quando não há dados é **200 com zeros** (documentado no Swagger).

## 4.5 Versionamento da API

Esta API usa **Asp.Versioning** com estratégia de **URL segment**.

- Formato: `api/v{version}/...`
- Versão padrão: `v1` (quando a versão não for especificada explicitamente)
- O Swagger UI lista automaticamente todas as versões disponíveis.

Exemplo:

- `POST /api/v1/lancamentos`

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