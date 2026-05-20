# ADR-0065: Workers dedicados no Docker Compose local

## Status

Aceito

## Contexto

O repositorio passou a ter hosts separados para `LedgerService.Worker` e `BalanceService.Worker`. O `compose.yaml` local ainda precisava refletir essa separacao para evitar que APIs HTTP e processamento assincrono fossem tratados como um unico processo operacional.

## Decisao

O `compose.yaml` local passa a subir quatro containers de aplicacao de negocio:

- `ledger-service`, usando `LedgerService.Api`;
- `ledger-worker`, usando `LedgerService.Worker`;
- `balance-service`, usando `BalanceService.Api`;
- `balance-worker`, usando `BalanceService.Worker`.

As APIs mantem apenas configuracoes HTTP, JWT, banco e observabilidade de API, com `ASPNETCORE_URLS` e portas publicadas no host. Os workers usam Generic Host, nao expoem porta HTTP e concentram as configuracoes de processamento em background:

- `ledger-worker`: produtor Kafka da Outbox, publisher de Outbox, processamento de estornos e consumer de reprocessamentos;
- `balance-worker`: consumer Kafka de eventos `LedgerEntryCreated.v1` e produtor DLQ.

## Consequencias

- A API do Ledger nao publica Outbox nem consome reprocessamentos no compose local.
- A API do Balance nao consome eventos Kafka no compose local.
- `ledger-worker` e `balance-worker` podem ser reiniciados, escalados ou diagnosticados separadamente das APIs HTTP.
- Logs e telemetria passam a identificar os processos com `ServiceName` distinto: `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`.
