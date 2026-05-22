# k6 load tests

Este diretorio contem scripts k6 usados pelos runners em `./scripts/run-loadtests.*`.

## Configuracao

A configuracao e carregada na ordem:

1. Variaveis de ambiente do k6 (`__ENV`)
2. Arquivo `.env.k6.auto` (gerado por `scripts/compose-env.*`)
3. Defaults (fallback para execucao local via `localhost`)

Os cenarios exigem `TOKEN` (JWT), a menos que `ALLOW_ANON=true`.

## Servicos necessarios

Execute a stack local antes dos testes. Os cenarios HTTP chamam apenas as APIs:

- `Auth.Api`, para emissao do token JWT;
- `LedgerService.Api`, para `POST /api/v1/lancamentos`;
- `BalanceService.Api`, para `GET /v1/consolidados/diario/{date}`.

Mantenha `LedgerService.Worker` e `BalanceService.Worker` em execucao quando quiser validar efeitos assincronos entre Outbox, Kafka e projecao de saldos. Os workers nao sao tratados como APIs HTTP.

O override `compose.k6.yaml` aumenta limites tecnicos de rate limiting das APIs durante a execucao de carga. Os runners recriam os containers HTTP alvo para garantir que os overrides e connection strings efetivos sejam aplicados. Isso evita que os cenarios de throughput validem apenas o limitador local, mantendo os asserts de status e erro HTTP.

Antes de executar o k6, os runners validam autenticacao real no PostgreSQL do Balance com as variaveis `BALANCE_DB_USER`, `BALANCE_DB_NAME` e `BALANCE_DB_PASSWORD`. Se o volume local tiver sido criado com senha antiga, o runner para antes do teste e aponta para o troubleshooting; nenhum volume e apagado automaticamente.
