# poc-arquitetura

## Outbox Publisher (Kafka) - LedgerService

O LedgerService usa o padrão **Outbox** para publicar eventos no Kafka com entrega **at-least-once**.
O endpoint (ex.: criação de lançamento) **não publica diretamente no Kafka**: ele grava o evento em `outbox_messages` com status `PENDING` e o **BackgroundService** publica em background.

### Configurar Kafka (local)

1. Suba um Kafka local em `localhost:9092` (ex.: via Docker Compose).
2. Configure no `src/LedgerService.Api/appsettings.json` (ou via variáveis de ambiente):

```json
{
  "Kafka": {
    "Producer": {
      "BootstrapServers": "localhost:9092",
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

### Como validar (PENDING -> SENT)

1. Aplique as migrations no PostgreSQL.
2. Suba a API (`dotnet run` no projeto `LedgerService.Api`).
3. Crie um lançamento via endpoint.
4. Verifique no banco:
   - ao criar, surge uma linha em `outbox_messages` com `status = 'Pending'`
   - após alguns segundos, o publisher marca como `status = 'Sent'` (após confirmação do publish no Kafka)

> Observação: em caso de falha no Kafka, o serviço não cai: ele registra erro, incrementa tentativas e agenda `next_attempt_at` com backoff.

## Migrations (Entity Framework Core)

### Pré-requisitos

- PostgreSQL rodando e acessível (veja `src/LedgerService.Api/appsettings*.json`).
- Tool local `dotnet-ef` (já versionada via `dotnet-tools.json`).

Para restaurar as tools locais:

```bash
dotnet tool restore
```

### Criar uma nova migration

As migrations ficam no projeto `LedgerService.Infrastructure` (onde está o `AppDbContext`).

```bash
dotnet tool run dotnet-ef -- migrations add NomeDaMigration \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -o Persistence\\Migrations
```

### Aplicar migrations no banco

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

> Observação: o `-- --environment Development` é repassado para a aplicação (startup project) para ela carregar `appsettings.Development.json`.