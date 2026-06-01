# Validacao final do backend

Data: 2026-06-01

Resultado: **passou com ressalvas**.

## Escopo validado

- Estado inicial limpo na branch `feature/otimizacoes`.
- Presenca dos 10 commits semanticos recentes das etapas de melhoria.
- Restore, build Release, suite completa de testes e filtro arquitetural.
- Formatacao em modo somente verificacao.
- Migration de timestamps UTC do LedgerService aplicada em PostgreSQL local.
- Rotas publicas do BalanceService, scripts e cenarios k6.
- Configuracao dos arquivos Compose e overlays.
- Stack core local, health, readiness, workers, Outbox, Kafka e consolidado.
- MediatR, boundaries arquiteturais e usos temporais problematicos.
- Logs operacionais basicos e nomes dos servicos observaveis.

## Comandos principais

```powershell
git status --short --branch
git log --oneline -10
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build
dotnet test ./LedgerService.slnx --configuration Release --no-build --filter Architecture
dotnet format ./LedgerService.slnx --verify-no-changes --no-restore
docker compose -f compose.yaml config --quiet
docker compose -f compose.yaml -f compose.nginx.yaml config --quiet
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth config --quiet
docker compose -f compose.yaml -f compose.observability.yaml --profile observability config --quiet
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config --quiet
docker compose -f compose.yaml up -d --build
dotnet ef database update --project src/LedgerService.Infrastructure --startup-project src/LedgerService.Api --context AppDbContext --connection "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=local_dev_password"
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-loadtests.ps1 -Mode smoke
docker compose -f compose.yaml down --remove-orphans
```

Tambem foram executadas buscas com `rg`, consultas `psql`, chamadas HTTP e um roteiro
PowerShell temporario em memoria para validar idempotencia e consistencia eventual.

## Resultados

| Validacao | Resultado |
| --- | --- |
| Restore | Passou. |
| Build Release | Passou com avisos de analyzers existentes. |
| Testes completos | Passou: 440 testes. |
| Testes arquiteturais | Passou: 10 testes no projeto `Architecture.Tests`; o filtro adicional tambem executou 39 testes relacionados no LedgerService. |
| Compose e overlays | Passou para core, Nginx, Auth legado, observabilidade e k6. |
| Health/readiness | Passou com HTTP 200 em LedgerService.Api e BalanceService.Api antes e depois do smoke k6. |
| Migration Ledger timestamptz | Passou. A migration `20260601105624_UseTimestampWithTimeZoneForLedgerTimestamps` foi aplicada com conversoes explicitas `USING ... AT TIME ZONE 'UTC'`. |
| Schema temporal Ledger | Passou: 19 colunas `timestamp with time zone` e 0 colunas `timestamp without time zone` no schema `public`. |
| Rotas Balance | Passou. A rota ativa e `/api/v1/consolidados/...`; nao foi encontrado uso indevido da rota antiga. |
| k6 | Passou. `ledger_resilience`: 24/24 checks; `balance_daily_50rps`: 11/11 checks, 0 falhas HTTP e 0 iteracoes descartadas. |
| Fluxo ponta a ponta | Passou. Criacao `201`, replay idempotente `201` com mesmo lancamento, payload divergente com mesma chave `409`, Outbox `Processed`, consumo em `processed_events` e consulta do consolidado `200`. |
| MediatR | Passou. Controllers usam `ISender`; services diretos restantes sao de autorizacao HTTP, nao casos de uso. |
| Boundaries | Passou nos testes e nas buscas complementares. |
| Tempo | Passou. Nao ha `DateTime.Now`, `DateTimeKind.Unspecified` nem configuracao atual `timestamp without time zone` nos fluxos ajustados do LedgerService. |
| ServiceName | Passou para `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`. |
| Encerramento | Passou com `down --remove-orphans`, sem remover volumes. |

## Evidencias funcionais

O roteiro ponta a ponta observou:

```text
create=201 replay=201 conflict=Conflict entry=lan_99aa7253
outbox=256dce75-5ee9-4453-8cef-e71ff016f343|LedgerEntryCreated.v1|Processed|2cbdd495-586f-4565-a807-c5dc6710d237
processed=lan_99aa7253|tese
balance=200
```

Os logs mostraram os processos `LedgerService.Worker` e `BalanceService.Worker`, o inicio
do `OutboxPublisherService`, o inicio do `LedgerEventsConsumer` e `CorrelationId` nas APIs.

## Ressalvas

1. `dotnet format --verify-no-changes` falhou por debito amplo de formatacao ja existente
   em varios arquivos. Nenhuma formatacao automatica foi aplicada para evitar churn fora
   desta etapa de validacao.
2. O primeiro `dotnet ef database update` usou o default local `127.0.0.1:5432` e falhou
   porque o Compose expoe Ledger PostgreSQL em `15432`. A repeticao com connection string
   explicita passou.
3. O smoke k6 emitiu aviso de compatibilidade futura para `open()` relativo em
   `loadtests/k6/lib/config.js`. O teste atual passou; vale ajustar em etapa separada.
4. A amostra de logs nao apresentou linhas semanticas explicitas de `publish processed`
   e `consumer commit`. Os estados persistidos da Outbox e do Balance comprovaram o fluxo,
   mas logs operacionais mais diretos melhorariam o diagnostico.
5. O ambiente Docker avisou que o kernel nao suporta limite de swap. Isso nao impediu a
   validacao.

## Arquivos gerados localmente

O smoke k6 criou `.env.k6.auto` e artefatos em `artifacts/k6/`. Ambos permanecem
ignorados pelo Git.
