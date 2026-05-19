# Troubleshooting

Este guia aponta os caminhos de diagnostico mais comuns sem duplicar os guias operacionais completos.

## Migrations falham

Confirme se o PostgreSQL do servico esta acessivel e se a connection string usa a porta correta do compose:

- Ledger: `localhost:15432`
- Balance: `localhost:15433`

Os comandos oficiais ficam em [migrations via compose](development/local-development.md#migrations-via-compose). Nao altere migrations antigas apenas para organizar.

## Testcontainers nao encontra Docker

Testes com Testcontainers precisam de uma Docker-compatible API acessivel. Eles nao dependem de Docker Desktop especificamente, mas precisam conseguir falar com a API do runtime.

Validacao rapida:

```powershell
docker version
docker ps
dotnet test
```

No Windows com Rancher Desktop, use `moby/dockerd`. Se `DOCKER_HOST=npipe:////./pipe/docker_engine` causar erro no Docker.DotNet, normalize apenas no processo do teste:

```powershell
$env:DOCKER_HOST = "npipe://./pipe/docker_engine"
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Detalhes ficam em [Testcontainers e Docker-compatible API](development/local-development.md#testcontainers-e-docker-compatible-api).

## Swagger nao abre

Swagger/OpenAPI fica habilitado por padrao somente em `Development`. Fora desse ambiente, a exposicao exige `Swagger:Enabled=true`.

Confirme tambem se a API correta esta rodando:

- Auth.Api: `http://localhost:5030/`
- LedgerService.Api: `http://localhost:5226/`
- BalanceService.Api: `http://localhost:5228/`

Veja [Swagger e endpoints operacionais](development/local-development.md#swagger-e-endpoints-operacionais).

## Readiness retorna 503

`GET /ready` valida dependencias obrigatorias do servico. Em geral, investigue:

- conexao com PostgreSQL;
- topicos Kafka esperados;
- `Kafka:Enabled`;
- connection strings e bootstrap servers usados no ambiente.

Detalhes ficam em [observabilidade](observability.md#readiness) e [Kafka, Outbox e DLQ](development/kafka-outbox.md).

## Outbox fica em Pending ou Failed

Mensagens `Pending` podem ser normais durante a janela de polling. Se permanecerem acumuladas ou chegarem a `Failed`, investigue Kafka, topic map, ACL/configuracao local, serializacao e `last_error`.

Use [Kafka, Outbox e DLQ](development/kafka-outbox.md#outbox) para entender estados, backoff e requeue operacional. Nao use requeue para mascarar erro permanente de contrato ou payload.

## Balance nao atualiza saldo

Confirme a cadeia completa:

1. lancamento criado no Ledger;
2. mensagem em `outbox_messages`;
3. status final `Sent`;
4. evento consumido em `processed_events`;
5. atualizacao em `daily_balances`;
6. ausencia de mensagem inesperada na DLQ.

O roteiro operacional completo fica em [validacao Auth -> Ledger -> Outbox -> Kafka -> Balance](observability.md#validacao-auth---ledger---outbox---kafka---balance).

## Token JWT e rejeitado

Confirme issuer, audience, scopes e merchant:

- `scope` deve conter o scope exigido pelo endpoint;
- `merchant_id` deve conter o merchant usado no body ou query;
- `aud` deve conter a audience da API;
- fora de `Development` e `Local`, JWKS deve usar HTTPS.

Veja [autenticacao e autorizacao](development/authentication.md).

## Grafana, Prometheus, Loki ou Jaeger nao mostram dados

Confirme se a stack local foi iniciada pelo script recomendado e se as portas estao livres:

- Jaeger UI: `http://localhost:16686/`
- Prometheus: `http://localhost:9090/`
- Loki: `http://localhost:3100/`
- Alertmanager: `http://localhost:9093/`
- Grafana: `http://localhost:3000/`

O desenho da stack e as validacoes ficam em [observabilidade](observability.md#configuracao-local).

## Load tests falham

Os testes k6 rodam em container dentro da rede do compose e exigem a stack local ativa. Comece pelo modo smoke:

```powershell
./scripts/run-loadtests.ps1 -Mode smoke
```

No Linux/macOS:

```bash
./scripts/run-loadtests.sh smoke
```

Detalhes ficam em [load tests com k6](development/local-development.md#load-tests-com-k6) e [loadtests/k6](../loadtests/k6/README.md).
