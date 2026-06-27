# k6 load tests

Este diretorio contem scripts k6 usados pelos runners em `./scripts/performance/run-loadtests.*`.

## Configuracao

A configuracao efetiva usa esta precedencia, da menor para a maior:

1. Defaults para execucao local via `localhost`
2. Arquivo `.env.k6.auto`, gerado por `scripts/lib/compose-env.*`
3. Variaveis de ambiente do k6, via `__ENV`

Os runners oficiais obtem `TOKEN` chamando `scripts/validation/get-token.*`; por padrao, o provider e o Keycloak local via `client_credentials`. Os cenarios tambem aceitam `ALLOW_ANON=true`, mas esse caminho nao e usado no fluxo oficial.

Variaveis padrao relevantes:

- `MESSAGING_PROVIDER=Kafka`
- `KAFKA_BOOTSTRAP_SERVERS=localhost:19092` fora do compose, ou `kafka:9092` dentro da rede Docker
- `BASE_URL_LEDGER` / `LEDGER_BASE_URL` conforme ambiente
- `BASE_URL_BALANCE` / `BALANCE_BASE_URL` conforme ambiente
- `BASE_URL_TRANSFER` / `TRANSFER_BASE_URL` conforme ambiente
- `BASE_URL_AUTH` / `AUTH_BASE_URL` quando um emissor legado for usado explicitamente
- `KEYCLOAK_BASE_URL` ou `KEYCLOAK_HOST_PORT`, para emissao local do token
- `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_CLIENT_SECRET` e `KEYCLOAK_SCOPE`, para client credentials do runner
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
| `transfer-circuit-breaker-kafka` (`transfer-cb-kafka`) | `transfer_circuit_breaker` | Validar degradacao e recuperacao do `TransferService.Worker` quando o `LedgerService.Api` fica indisponivel. | Fase saudavel: 1 iteracao com timeout ate 120s. Fase degradada: 5 VUs por 30s. Fase recuperacao: 1 iteracao com timeout ate 120s. | POST/GET do `TransferService.Api` continuam autenticados e rapidos, degradacao e classificada sem tempestade de erro HTTP, logs do worker indicam circuito aberto, chamadas rejeitadas por circuito aberto, half-open e fechamento apos restaurar o Ledger. |

Os nomes antigos entre parenteses continuam aceitos por compatibilidade. Para novos comandos, prefira os nomes com sufixo `-kafka`.

## Exemplos

```powershell
./scripts/local/start-stack.ps1
./scripts/performance/run-loadtests.ps1 -Mode smoke-kafka
./scripts/performance/run-loadtests.ps1 -Mode load-kafka
./scripts/performance/run-loadtests.ps1 -Mode transfer-fullstack-kafka
./scripts/performance/run-loadtests.ps1 -Mode transfer-circuit-breaker-kafka
```

```bash
./scripts/local/start-stack.sh
./scripts/performance/run-loadtests.sh smoke-kafka
./scripts/performance/run-loadtests.sh load-kafka
./scripts/performance/run-loadtests.sh transfer-fullstack-kafka
./scripts/performance/run-loadtests.sh transfer-circuit-breaker-kafka
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
| `transfer-smoke-kafka` | `TRANSFER` | 10000ms | 15000ms |
| `transfer-load-kafka` | `TRANSFER` | 1000ms | 2000ms |
| `transfer-fullstack-kafka` | `TRANSFER` | 1000ms | 2000ms |
| `transfer-circuit-breaker-kafka` | `TRANSFER` | 1000ms | 2000ms |

Para sobrescrever limites sem alterar codigo:

```powershell
$env:BALANCE_HTTP_REQ_DURATION_P95_MS='1500'
$env:BALANCE_HTTP_REQ_DURATION_P99_MS='3500'
./scripts/performance/run-loadtests.ps1 -Mode load-kafka
```

```bash
BALANCE_HTTP_REQ_DURATION_P95_MS=1500 BALANCE_HTTP_REQ_DURATION_P99_MS=3500 ./scripts/performance/run-loadtests.sh load-kafka
```

Tambem existe override global para os dois cenarios quando o prefixo especifico nao for informado: `K6_HTTP_REQ_DURATION_P95_MS` e `K6_HTTP_REQ_DURATION_P99_MS`.

## Servicos Necessarios

Execute a stack local Kafka antes dos testes. Os runners precisam de:

- Keycloak, para emissao do token JWT padrao usado pelos runners;
- Kafka e `kafka-init-topics`, para o caminho padrao `MESSAGING_PROVIDER=Kafka`;
- `LedgerService.Api` e `LedgerService.Worker`, para criar lancamentos e publicar Outbox;
- `BalanceService.Api` e `BalanceService.Worker`, para consultar e projetar saldos;
- `TransferService.Api`, para os modos `transfer-smoke-kafka`, `transfer-load-kafka` e `transfer-fullstack-kafka`;
- `TransferService.Worker` e `LedgerService.Api`, para `transfer-fullstack-kafka` e `transfer-circuit-breaker-kafka`.

No `transfer-fullstack-kafka`, o `TransferService.Worker` precisa autenticar no `LedgerService.Api` por OAuth2 client credentials. No compose local isso usa `KEYCLOAK_CLIENT_ID=poc-automation`, `KEYCLOAK_CLIENT_SECRET` e `TRANSFER_WORKER_LEDGER_AUTH_SCOPE=ledger.write`. Uma Saga `Failed` por `401 Unauthorized` deve falhar o smoke e indica problema real nessa configuracao, no token emitido ou na validacao JWT do Ledger.

No `transfer-circuit-breaker-kafka`, o runner:

- executa uma fase saudavel para confirmar que transferencias chegam em `Completed`;
- para o container `ledger-service` de forma controlada;
- executa uma fase degradada criando transferencias enquanto o worker tenta chamar o Ledger;
- valida nos logs do `transfer-worker` as mensagens `Circuit breaker HTTP aberto. Client=Ledger` e `Chamada HTTP rejeitada por circuito aberto. Client=Ledger`;
- restaura o `ledger-service`;
- executa uma fase de recuperacao e valida `Tentativa HTTP em half-open liberada pelo circuit breaker. Client=Ledger` e `Circuit breaker HTTP fechado. Client=Ledger`.

Esse cenario nao altera contratos publicos das APIs. Como a falha ocorre no worker, o k6 valida que o `TransferService.Api` continua aceitando `POST /api/v1/transferencias`, respondendo `GET /api/v1/transferencias/{id}` e classificando a degradacao sem falha de autenticacao. A evidencia direta do Circuit Breaker vem dos logs e das metricas OpenTelemetry do worker.

Overrides uteis para regressao local:

```powershell
$env:TRANSFER_CIRCUIT_DEGRADED_VUS='8'
$env:TRANSFER_CIRCUIT_DEGRADED_DURATION='45s'
$env:TRANSFER_CIRCUIT_RECOVERY_DURATION='1m'
$env:K6_LEDGER_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT='2'
$env:K6_LEDGER_CIRCUIT_BREAKER_BREAK_DURATION='00:00:05'
./scripts/performance/run-loadtests.ps1 -Mode transfer-circuit-breaker-kafka
```

```bash
TRANSFER_CIRCUIT_DEGRADED_VUS=8 \
TRANSFER_CIRCUIT_DEGRADED_DURATION=45s \
TRANSFER_CIRCUIT_RECOVERY_DURATION=1m \
K6_LEDGER_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT=2 \
K6_LEDGER_CIRCUIT_BREAKER_BREAK_DURATION=00:00:05 \
./scripts/performance/run-loadtests.sh transfer-circuit-breaker-kafka
```

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

As validacoes Kafka ficam no runner Docker, nao no codigo k6. Os cenarios k6 continuam focados em contrato HTTP, carga e thresholds. No `transfer-fullstack-kafka`, os thresholds de latencia sao opcionais porque o cenario sobe/usa a stack local e executa uma iteracao funcional sujeita a cold start; para habilita-los explicitamente, defina `TRANSFER_FULLSTACK_ENFORCE_LATENCY_THRESHOLDS=true`.

## Observacoes

Antes de `load-kafka`, os runners aguardam a drenagem do fluxo assincrono local de `LedgerEntryCreated.v1`/`LedgerEntryCreated.v2` para evitar medir leitura do Balance enquanto ha backlog de cenarios anteriores.

Os cenarios `constant-arrival-rate` controlam taxa de chegada; `sleep()` dentro da iteracao nao define RPS. Nos cenarios `constant-vus`, `sleep()` representa think time simples.

Os testes validam comportamento local/controlado da POC. Eles nao comprovam capacidade produtiva, autoscaling, limites de infraestrutura, comportamento multi-tenant real, seguranca de borda ou resiliencia sob falhas de dependencias gerenciadas.
