# k6 load tests

Este diretorio contem scripts k6 usados pelos runners em `./scripts/run-loadtests.*`.

## Configuracao

A configuracao e carregada na ordem:

1. Variaveis de ambiente do k6 (`__ENV`)
2. Arquivo `.env.k6.auto` (gerado por `scripts/compose-env.*`)
3. Defaults (fallback para execucao local via `localhost`)

Os cenarios exigem `TOKEN` (JWT), a menos que `ALLOW_ANON=true`. Os runners oficiais (`scripts/run-loadtests.*`) obtêm esse token antes de iniciar o k6 chamando `scripts/get-token.*`; por padrão o provider é o Keycloak local via `client_credentials`.

## Modos e criterios de aceite

Os runners `./scripts/run-loadtests.ps1` e `./scripts/run-loadtests.sh` expoem tres modos. Eles executam k6 dentro da rede Docker Compose e exportam summaries JSON em `artifacts/k6`.

| Modo | Cenario k6 | Objetivo | Carga padrao | Duracao padrao | Criterios de aceite atuais |
| --- | --- | --- | --- | --- | --- |
| `smoke` | `ledger_resilience` e `balance_daily_50rps` | Validar que Ledger e Balance respondem no ambiente local e que ao menos um evento percorre Outbox -> Pub/Sub emulator -> Balance antes de cenarios maiores. | Ledger: 1 VU constante. Balance: 1 req/s, 5 VUs pre-alocados e maximo 10 VUs. | 10s por cenario. | Sem checks falhos, `checks == 100%`, `http_req_failed <= 0.05`, `dropped_iterations == 0`, `http_req_duration p95 < 3000ms`, `http_req_duration p99 < 6000ms` e incremento de Outbox processada e `processed_events` no Balance. |
| `balance50` | `balance_daily_50rps` | Exercitar consulta de consolidado diario no BalanceService.Api com taxa constante controlada. | 50 req/s, 50 VUs pre-alocados e maximo 200 VUs. | 1m. | Sem checks falhos, `checks == 100%`, `http_req_failed <= 0.05`, `dropped_iterations == 0`, `http_req_duration p95 < 1000ms` e `http_req_duration p99 < 2500ms`. |
| `resilience` | `ledger_resilience` | Exercitar criacao de lancamentos no LedgerService.Api com idempotency key e correlation id por iteracao. | 5 VUs constantes. | 1m. | Sem checks falhos, `checks == 100%`, `http_req_failed <= 0.05`, `dropped_iterations == 0`, `http_req_duration p95 < 2000ms` e `http_req_duration p99 < 5000ms`. |

`smoke` e uma validacao curta de sanidade local. `balance50` e um cenario de throughput de leitura controlado para o Balance. `resilience` e um cenario de escrita concorrente no Ledger, com foco em aceitar chamadas validas sob carga moderada local.

Os scripts k6 declaram thresholds para `checks`, `dropped_iterations` e para `http_req_failed`/`http_req_duration` filtrados por operacao. As chamadas HTTP usam tags `name`, `service` e `operation`, permitindo separar no summary operacoes como `ledger_create_entry` e `balance_daily_summary`. Os runners tambem conferem o summary JSON para manter diagnostico explicito de checks falhos, taxa de erro HTTP e iteracoes descartadas.

`iteration_duration` continua observacional nesta etapa. Ela aparece no summary do k6, mas mistura tempo de API, `sleep()` do script e comportamento do executor, entao ainda nao e um bom gate para esta POC.

Em cenarios `constant-arrival-rate`, o executor controla a taxa de chegada configurada; `sleep()` dentro da iteracao nao define o RPS e pode apenas aumentar a duracao da iteracao, exigindo mais VUs ou contribuindo para `dropped_iterations`. Em cenarios `constant-vus`, `sleep()` reduz diretamente o ritmo de cada VU e pode representar think time simples quando essa intencao estiver clara.

## Thresholds iniciais de latencia

Os limites sao guardrails iniciais para ambiente local controlado, nao SLO produtivo. Eles foram escolhidos com folga para variacao de maquina, Docker, PostgreSQL, Keycloak, Pub/Sub emulator e runners locais.

| Modo | Prefixo de override | p95 padrao | p99 padrao | Observacao |
| --- | --- | --- | --- | --- |
| `smoke` Ledger | `LEDGER` | 3000ms | 6000ms | Sanidade curta com 1 VU e validacao do fluxo assincrono apos o k6. |
| `smoke` Balance | `BALANCE` | 3000ms | 6000ms | Sanidade curta de leitura com 1 req/s. |
| `balance50` | `BALANCE` | 1000ms | 2500ms | Leitura HTTP do consolidado diario a 50 req/s em stack local aquecida. |
| `resilience` | `LEDGER` | 2000ms | 5000ms | Escrita HTTP moderada com persistencia, idempotency key, correlation id e Outbox. |

Para sobrescrever os limites sem alterar codigo, informe variaveis no processo que executa o k6:

```powershell
$env:BALANCE_HTTP_REQ_DURATION_P95_MS='1500'
$env:BALANCE_HTTP_REQ_DURATION_P99_MS='3500'
./scripts/run-loadtests.ps1 -Mode balance50
```

```bash
BALANCE_HTTP_REQ_DURATION_P95_MS=1500 BALANCE_HTTP_REQ_DURATION_P99_MS=3500 ./scripts/run-loadtests.sh balance50
```

Tambem existe override global para os dois cenarios quando o prefixo especifico nao for informado: `K6_HTTP_REQ_DURATION_P95_MS` e `K6_HTTP_REQ_DURATION_P99_MS`.

Uma falha de threshold deve ser lida como sinal de regressao local ou de ambiente instavel. Antes de endurecer ou afrouxar os valores, confirme:

- stack subida pelo fluxo documentado;
- containers recriados pelo runner k6;
- banco local sem backlog incomum;
- maquina sem carga concorrente pesada;
- primeira execucao descartada quando houver cold start relevante;
- summaries JSON preservados em `artifacts/k6`.

Para coletar uma nova linha de base local, rode cada modo ao menos tres vezes em uma stack recem-subida e aquecida, compare `http_req_duration`, `http_req_failed`, `checks`, `dropped_iterations` e `iteration_duration` nos summaries, e registre a justificativa quando ajustar os thresholds. Ajustes devem continuar separando baseline local de objetivo produtivo.

Estes testes validam comportamento local/controlado da POC. Eles nao comprovam capacidade produtiva, dimensionamento, autoscaling, limites de infraestrutura, comportamento multi-tenant real, seguranca de borda ou resiliencia sob falhas de dependencias gerenciadas.

## Servicos necessarios

Execute a stack local antes dos testes. Os runners precisam do Keycloak local para obter o token padrão e os cenarios HTTP chamam apenas as APIs de negócio:

- Keycloak, para emissão do token JWT padrão usado pelos runners;
- `LedgerService.Api`, para `POST /api/v1/lancamentos`;
- `BalanceService.Api`, para `GET /api/v1/consolidados/diario/{date}`.

`Auth.Api` permanece disponível somente como fallback de transição quando `TOKEN_PROVIDER=auth-api` também estiver acompanhado da configuração JWT legada das APIs, conforme `docs/development/authentication.md`.

Os runners exigem `pubsub-emulator`, `LedgerService.Worker` e `BalanceService.Worker` em execucao e confirmam que os workers usam `Messaging__Provider=PubSub` com `PUBSUB_EMULATOR_HOST=pubsub-emulator:8085`. Isso impede que o fluxo local publique contra Pub/Sub real por acidente. Os workers nao sao tratados como APIs HTTP.

## Mensageria, Outbox e cenarios futuros

Os cenarios k6 atuais exercitam as APIs HTTP e podem gerar backlog na Outbox. O modo `smoke` aguarda ao menos uma publicacao e projecao via Pub/Sub emulator; ele nao valida diretamente `ack`/`nack`, commit Kafka legado, DLQ ou idempotencia do consumer.

Para carga focada no fluxo assincrono, mantenha Pub/Sub emulator como provider local principal e acompanhe banco, logs e metricas dos workers. Use Kafka legado apenas quando o cenario depender desse adapter. Cenarios recomendados para evolucao futura:

- alto volume de criacao de lancamentos no `LedgerService.Api`;
- crescimento e drenagem de `outbox_messages` por status;
- publicacao em lote pelo `OutboxPublisherService` via `IOutboxMessagePublisher`;
- consumo pelo `BalanceService.Worker` apos mapeamento do provider para `ReceivedMessage`;
- duplicidade de mensagens e idempotencia em `processed_events`;
- falhas temporarias do broker atual e efeito em retry/backoff;
- DLQ por payload invalido ou contrato rejeitado.

Os adapters Pub/Sub tratam `ack`/`nack`, subscription, delivery attempt e ordering key como detalhes desse provider, sem usar partition/offset/commit como expectativa generica. O smoke k6 confirma o efeito ponta a ponta do transporte local, sem inspecionar internals do emulator.

O override `compose.k6.yaml` aumenta limites tecnicos de rate limiting das APIs durante a execucao de carga. Os runners recriam os containers HTTP alvo para garantir que os overrides e connection strings efetivos sejam aplicados. Isso evita que os cenarios de throughput validem apenas o limitador local, mantendo os asserts de status e erro HTTP.

Antes de executar o k6, os runners validam autenticacao real no PostgreSQL do Balance com as variaveis `BALANCE_DB_USER`, `BALANCE_DB_NAME` e `BALANCE_DB_PASSWORD`. Se o volume local tiver sido criado com senha antiga, o runner para antes do teste e aponta para o troubleshooting; nenhum volume e apagado automaticamente.

## Ledger via Nginx

Os runners k6 continuam apontando para as APIs HTTP diretas do `compose.yaml` por padrao, inclusive `BASE_URL_LEDGER=http://ledger-service:8080` dentro da rede Docker. Essa escolha preserva os cenarios existentes e evita misturar teste de carga funcional com a demonstracao local de borda.

O load balance local do Ledger e os limites defensivos do Nginx no `compose.nginx.yaml` devem ser validados pelos comandos documentados em `docs/development/local-development.md`, usando `https://ledger.localhost:7443`, os campos `X-Upstream-Addr`/`upstream_addr` e os status `413`/`429` esperados. Um cenario k6 dedicado ao Nginx pode ser criado futuramente se a POC precisar medir comportamento do proxy como alvo de carga; nesse caso, `429` por excesso de requisicoes deve ser tratado como resultado esperado da borda, nao como regressao das APIs.
