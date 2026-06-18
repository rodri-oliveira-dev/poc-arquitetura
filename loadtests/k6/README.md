# k6 load tests

Este diretorio contem scripts k6 usados pelos runners em `./scripts/run-loadtests.*`.

## Configuracao

A configuracao efetiva usa esta precedencia, da menor para a maior:

1. Defaults para execucao local via `localhost`
2. Arquivo `.env.k6.auto`, gerado por `scripts/compose-env.*`
3. Variaveis de ambiente do k6, via `__ENV`

Os runners oficiais obtem `TOKEN` chamando `scripts/get-token.*`; por padrao, o provider e o Keycloak local via `client_credentials`. Os cenarios tambem aceitam `ALLOW_ANON=true`, mas esse caminho nao e usado no fluxo oficial.

Variaveis padrao relevantes:

- `MESSAGING_PROVIDER=Kafka`
- `KAFKA_BOOTSTRAP_SERVERS=localhost:19092` fora do compose, ou `kafka:9092` dentro da rede Docker
- `BASE_URL_LEDGER` / `LEDGER_BASE_URL` conforme ambiente
- `BASE_URL_BALANCE` / `BALANCE_BASE_URL` conforme ambiente
- `BASE_URL_TRANSFER` / `TRANSFER_BASE_URL` conforme ambiente
- `BASE_URL_AUTH` / `AUTH_BASE_URL` quando um emissor legado for usado explicitamente
- credenciais de client por ambiente via `.env.local` ou variaveis de processo, sem versionar segredo

## Modos

Os runners usam Kafka por padrao. Pub/Sub permanece legado/opt-in para a stack local, mas nao faz parte do caminho padrao de smoke/load k6.

| Modo | Cenario k6 | Objetivo | Carga padrao | Criterios de aceite |
| --- | --- | --- | --- | --- |
| `smoke-kafka` (`smoke`) | `ledger_resilience` e `balance_daily_50rps` | Validar Ledger, Kafka e Balance no fluxo curto. | Ledger: 1 VU por 10s. Balance: 1 req/s por 10s. | Sem checks falhos, erro HTTP <= 5%, sem iteracoes descartadas, Outbox processada, `processed_events` no Balance, `ledger.ledgerentry.created` cresce e DLQ `ledger.ledgerentry.created.dlq` nao cresce. |
| `load-kafka` | `balance_daily_50rps` | Exercitar leitura do consolidado diario com stack Kafka aquecida em carga leve. | 5 req/s por 30s. | Sem checks falhos, erro HTTP <= 5%, sem iteracoes descartadas, p95 < 1000ms e p99 < 2500ms. |
| `balance-load-kafka` (`balance50`) | `balance_daily_50rps` | Exercitar leitura do consolidado diario com taxa constante mais agressiva. | 50 req/s por 1m. | Sem checks falhos, erro HTTP <= 5%, sem iteracoes descartadas, p95 < 1000ms e p99 < 2500ms. |
| `ledger-load-kafka` (`resilience`) | `ledger_resilience` | Exercitar criacao de lancamentos com idempotency key e correlation id por iteracao. | 5 VUs por 1m. | Sem checks falhos, erro HTTP <= 5%, sem iteracoes descartadas, p95 < 2000ms e p99 < 5000ms. |
| `transfer-smoke-kafka` (`transfer-smoke`) | `transfer_smoke` | Validar contrato HTTP basico do `TransferService.Api`. | 1 VU, 1 iteracao. | `POST /api/v1/transferencias` retorna 202, `GET` retorna a Saga, replay idempotente retorna 202, conflito retorna 409, alem de 400/401/403/404 esperados. |
| `transfer-load-kafka` (`transfer-load`) | `transfer_load` | Exercitar POST/GET de transferencias com concorrencia moderada, sem exigir conclusao full-stack em todas as iteracoes. | Ramping ate 10 VUs. | `http_req_failed{service:transfer} < 2%`, checks >= 99%, POST/GET >= 99%, sem iteracoes descartadas, p95 < 1000ms e p99 < 2000ms. |
| `transfer-fullstack-kafka` | `transfer_fullstack_kafka` | Validar API + Worker + LedgerService + Outbox + Kafka no fluxo feliz da Saga. | 1 VU, 1 iteracao. | POST retorna 202, GET via polling chega em `Completed`, topicos principais crescem, `message key = transferenciaId`, `correlationId` esperado aparece no payload e DLQ nao cresce. |

Os nomes antigos entre parenteses continuam aceitos por compatibilidade. Para novos comandos, prefira os nomes com sufixo `-kafka`.

## Exemplos

```powershell
./scripts/start-local-stack.ps1
./scripts/run-loadtests.ps1 -Mode smoke-kafka
./scripts/run-loadtests.ps1 -Mode load-kafka
./scripts/run-loadtests.ps1 -Mode transfer-fullstack-kafka
```

```bash
./scripts/start-local-stack.sh
./scripts/run-loadtests.sh smoke-kafka
./scripts/run-loadtests.sh load-kafka
./scripts/run-loadtests.sh transfer-fullstack-kafka
```

Pub/Sub nao e usado no caminho padrao. Se alguem tentar `--provider PubSub` ou `-Provider PubSub`, o runner falha cedo com mensagem clara e aponta para a stack legada manual.

## Thresholds

Os limites sao guardrails iniciais para ambiente local controlado, nao SLO produtivo. Eles foram escolhidos com folga para variacao de maquina, Docker, PostgreSQL, Keycloak, Kafka e runners locais.

| Modo | Prefixo de override | p95 padrao | p99 padrao |
| --- | --- | --- | --- |
| `smoke-kafka` Ledger | `LEDGER` | 3000ms | 6000ms |
| `smoke-kafka` Balance | `BALANCE` | 3000ms | 6000ms |
| `load-kafka` | `BALANCE` | 1000ms | 2500ms |
| `balance-load-kafka` | `BALANCE` | 1000ms | 2500ms |
| `ledger-load-kafka` | `LEDGER` | 2000ms | 5000ms |
| `transfer-smoke-kafka` | `TRANSFER` | 500ms | 1000ms |
| `transfer-load-kafka` | `TRANSFER` | 1000ms | 2000ms |
| `transfer-fullstack-kafka` | `TRANSFER` | 1000ms | 2000ms |

Para sobrescrever limites sem alterar codigo:

```powershell
$env:BALANCE_HTTP_REQ_DURATION_P95_MS='1500'
$env:BALANCE_HTTP_REQ_DURATION_P99_MS='3500'
./scripts/run-loadtests.ps1 -Mode load-kafka
```

```bash
BALANCE_HTTP_REQ_DURATION_P95_MS=1500 BALANCE_HTTP_REQ_DURATION_P99_MS=3500 ./scripts/run-loadtests.sh load-kafka
```

Tambem existe override global para os dois cenarios quando o prefixo especifico nao for informado: `K6_HTTP_REQ_DURATION_P95_MS` e `K6_HTTP_REQ_DURATION_P99_MS`.

## Servicos Necessarios

Execute a stack local Kafka antes dos testes. Os runners precisam de:

- Keycloak, para emissao do token JWT padrao usado pelos runners;
- Kafka e `kafka-init-topics`, para o caminho padrao `MESSAGING_PROVIDER=Kafka`;
- `LedgerService.Api` e `LedgerService.Worker`, para criar lancamentos e publicar Outbox;
- `BalanceService.Api` e `BalanceService.Worker`, para consultar e projetar saldos;
- `TransferService.Api`, para os modos `transfer-smoke-kafka`, `transfer-load-kafka` e `transfer-fullstack-kafka`;
- `TransferService.Worker` e `LedgerService.Api`, apenas para `transfer-fullstack-kafka`.

Os runners falham cedo quando Kafka esta indisponivel, quando um servico obrigatorio nao esta em execucao ou quando `kafka-init-topics` nao concluiu. Se um topico estiver ausente, a mensagem sugere subir a stack local para executar o script de criacao de topicos.

## Validacao Kafka

No smoke Ledger/Balance, o runner:

- cria lancamento pelo `LedgerService.Api`;
- aguarda polling curto ate Outbox processada e projecao do Balance atualizada;
- confirma crescimento de `ledger.ledgerentry.created`;
- confirma que `ledger.ledgerentry.created.dlq` nao cresceu no fluxo feliz.

No `transfer-fullstack-kafka`, o runner:

- cria uma transferencia com `TRANSFER_CORRELATION_ID` controlado;
- consulta a Saga por HTTP ate `Completed`;
- valida os topicos `transfer.transferencia.solicitada`, `transfer.transferencia.debito-criado`, `transfer.transferencia.credito-criado` e `transfer.transferencia.concluida`;
- confirma que a message key de cada evento e igual ao `transferenciaId`;
- confirma que o payload contem o `correlationId` esperado;
- confirma que `transfer.transferencia.dlq` nao cresceu.

As validacoes Kafka ficam no runner Docker, nao no codigo k6. Os cenarios k6 continuam focados em contrato HTTP, carga e thresholds.

## Observacoes

Antes de `load-kafka`, os runners aguardam a drenagem do fluxo assincrono local de `LedgerEntryCreated.v1`/`LedgerEntryCreated.v2` para evitar medir leitura do Balance enquanto ha backlog de cenarios anteriores.

Os cenarios `constant-arrival-rate` controlam taxa de chegada; `sleep()` dentro da iteracao nao define RPS. Nos cenarios `constant-vus`, `sleep()` representa think time simples.

Os testes validam comportamento local/controlado da POC. Eles nao comprovam capacidade produtiva, autoscaling, limites de infraestrutura, comportamento multi-tenant real, seguranca de borda ou resiliencia sob falhas de dependencias gerenciadas.
