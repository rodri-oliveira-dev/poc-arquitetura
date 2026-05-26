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
| `smoke` | `ledger_resilience` e `balance_daily_50rps` | Validar que Ledger e Balance respondem no ambiente local com carga minima antes de cenarios maiores. | Ledger: 1 VU constante. Balance: 1 req/s, 5 VUs pre-alocados e maximo 10 VUs. | 10s por cenario. | Sem checks falhos, `http_req_failed <= 0.05` e `dropped_iterations == 0` no summary validado pelo runner. |
| `balance50` | `balance_daily_50rps` | Exercitar consulta de consolidado diario no BalanceService.Api com taxa constante controlada. | 50 req/s, 50 VUs pre-alocados e maximo 200 VUs. | 1m. | Sem checks falhos, `http_req_failed <= 0.05` e `dropped_iterations == 0`. O script tambem declara thresholds k6 para `http_req_failed` e `dropped_iterations`. |
| `resilience` | `ledger_resilience` | Exercitar criacao de lancamentos no LedgerService.Api com idempotency key e correlation id por iteracao. | 5 VUs constantes. | 1m. | Sem checks falhos, `http_req_failed <= 0.05` e `dropped_iterations == 0` no summary validado pelo runner. O script declara threshold k6 para `http_req_failed`. |

`smoke` e uma validacao curta de sanidade local. `balance50` e um cenario de throughput de leitura controlado para o Balance. `resilience` e um cenario de escrita concorrente no Ledger, com foco em aceitar chamadas validas sob carga moderada local.

Os runners falham quando ha qualquer check k6 falho, taxa de erro HTTP acima de 5% ou `dropped_iterations` maior que zero. Para `balance_daily_50rps.js`, o threshold de `dropped_iterations` tambem esta declarado no script. Para `ledger_resilience.js`, o runner valida `dropped_iterations` pelo summary exportado.

Os scripts atuais nao possuem threshold formal para latencia p95 ou p99. A recomendacao futura e definir limites por cenario apenas depois de registrar uma linha de base local reprodutivel, separando latencia de API, banco, Kafka, workers e runtime Docker.

Estes testes validam comportamento local/controlado da POC. Eles nao comprovam capacidade produtiva, dimensionamento, autoscaling, limites de infraestrutura, comportamento multi-tenant real, seguranca de borda ou resiliencia sob falhas de dependencias gerenciadas.

## Servicos necessarios

Execute a stack local antes dos testes. Os runners precisam do Keycloak local para obter o token padrão e os cenarios HTTP chamam apenas as APIs de negócio:

- Keycloak, para emissão do token JWT padrão usado pelos runners;
- `LedgerService.Api`, para `POST /api/v1/lancamentos`;
- `BalanceService.Api`, para `GET /v1/consolidados/diario/{date}`.

`Auth.Api` permanece disponível somente como fallback de transição quando `TOKEN_PROVIDER=auth-api` também estiver acompanhado da configuração JWT legada das APIs, conforme `docs/development/authentication.md`.

Mantenha `LedgerService.Worker` e `BalanceService.Worker` em execucao quando quiser validar efeitos assincronos entre Outbox, Kafka e projecao de saldos. Os workers nao sao tratados como APIs HTTP.

O override `compose.k6.yaml` aumenta limites tecnicos de rate limiting das APIs durante a execucao de carga. Os runners recriam os containers HTTP alvo para garantir que os overrides e connection strings efetivos sejam aplicados. Isso evita que os cenarios de throughput validem apenas o limitador local, mantendo os asserts de status e erro HTTP.

Antes de executar o k6, os runners validam autenticacao real no PostgreSQL do Balance com as variaveis `BALANCE_DB_USER`, `BALANCE_DB_NAME` e `BALANCE_DB_PASSWORD`. Se o volume local tiver sido criado com senha antiga, o runner para antes do teste e aponta para o troubleshooting; nenhum volume e apagado automaticamente.

## Ledger via Nginx

Os runners k6 continuam apontando para as APIs HTTP diretas do `compose.yaml` por padrao, inclusive `BASE_URL_LEDGER=http://ledger-service:8080` dentro da rede Docker. Essa escolha preserva os cenarios existentes e evita misturar teste de carga funcional com a demonstracao local de borda.

O load balance local do Ledger e os limites defensivos do Nginx no `compose.nginx.yaml` devem ser validados pelos comandos documentados em `docs/development/local-development.md`, usando `https://ledger.localhost:7443`, os campos `X-Upstream-Addr`/`upstream_addr` e os status `413`/`429` esperados. Um cenario k6 dedicado ao Nginx pode ser criado futuramente se a POC precisar medir comportamento do proxy como alvo de carga; nesse caso, `429` por excesso de requisicoes deve ser tratado como resultado esperado da borda, nao como regressao das APIs.
