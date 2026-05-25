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

Quando estiver usando a borda local com Nginx, confirme tambem:

- o certificado local existe em `infra/nginx/certs/localhost.crt` e `infra/nginx/certs/localhost.key`;
- o certificado possui SAN para `localhost`, `ledger.localhost`, `balance.localhost` e `auth.localhost`;
- o overlay foi aplicado no comando: `docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge`;
- os logs do Nginx nao mostram erro de certificado ou upstream: `docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge`;
- as URLs via proxy usam subdominio, nao path: `https://ledger.localhost:7443/swagger`, `https://balance.localhost:7443/swagger` e `https://auth.localhost:7443/swagger`.

Veja [Swagger e endpoints operacionais](development/local-development.md#swagger-e-endpoints-operacionais).

## Nginx local nao inicia

O Nginx local e opcional e depende dos arquivos de certificado montados por volume. Se o container `poc-nginx-edge` sair imediatamente, valide a configuracao efetiva:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml config
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Erros comuns:

- `cannot load certificate`: gere `infra/nginx/certs/localhost.crt`;
- `cannot load certificate key`: gere `infra/nginx/certs/localhost.key`;
- alerta de certificado no navegador: confie o certificado local ou use `mkcert -install`;
- `connection refused` ao abrir Swagger via Nginx: confirme se `ledger-service-1`, `ledger-service-2`, `balance-service` e `auth-api` estao em execucao e saudaveis.

O Nginx nao altera as portas HTTP diretas. Se precisar isolar o problema, valide primeiro a Swagger UI direta em `http://localhost:5226/index.html`, `http://localhost:5228/index.html` e `http://localhost:5030/index.html`, ou os documentos OpenAPI em `/swagger/v1/swagger.json`.

## HSTS aparece via Nginx local

A borda local nao deve devolver `Strict-Transport-Security`, mesmo quando uma API interna emitir esse header. O Nginx local aceita apenas `TLSv1.2` e `TLSv1.3`, mas HSTS fica fora do fluxo de desenvolvimento porque navegadores podem cachear a politica para `localhost` ou subdominios `.localhost`, especialmente com certificados autoassinados.

Valide os headers:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
```

Se `Strict-Transport-Security` aparecer, remova a configuracao local que adicionou esse header e recrie o container `nginx-edge`.

## Headers de seguranca nao aparecem via Nginx local

A borda local deve devolver `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` e `X-XSS-Protection` em portal, Swaggers e APIs acessados por `https://*:7443`. O portal em `https://localhost:7443` tambem deve devolver `Content-Security-Policy`; os hosts de Swagger nao aplicam CSP na borda para preservar a Swagger UI.

Valide:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://auth.localhost:7443/swagger
```

Se os headers nao aparecerem, recrie o container com o overlay e confirme que `infra/nginx/security-headers.conf` foi montado:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
```

## Cache-Control nao aparece via Nginx local

Os hosts de API via Nginx devem devolver `Cache-Control: no-store`, `Pragma: no-cache` e `Expires: 0` para evitar cache indevido de respostas sensiveis. Essa politica se aplica a `ledger.localhost`, `balance.localhost` e `auth.localhost`; o portal estatico em `https://localhost:7443` fica fora da regra.

Valide:

```bash
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://auth.localhost:7443/swagger
curl -k -I https://ledger.localhost:7443/health
```

Se os headers nao aparecerem, recrie o container com o overlay e valide a configuracao carregada:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
```

## Nginx nao distribui chamadas do Ledger

No overlay `compose.nginx.yaml`, o Nginx balanceia apenas `ledger.localhost:7443` entre `ledger-service-1:8080` e `ledger-service-2:8080`, usando `least_conn`. Ele nao usa o servico direto `ledger-service`.

Valide a configuracao efetiva:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml config
docker compose -f compose.yaml -f compose.nginx.yaml ps ledger-service-1 ledger-service-2 nginx-edge
```

Faça chamadas repetidas e confira o upstream:

```bash
for i in $(seq 1 20); do curl -k -s -o /dev/null -D - https://ledger.localhost:7443/health | grep -i X-Upstream-Addr; done
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge | grep upstream_addr
docker compose -f compose.yaml -f compose.nginx.yaml logs ledger-service-1 ledger-service-2
```

Em Windows PowerShell, use:

```powershell
1..20 | ForEach-Object {
  (Invoke-WebRequest -SkipCertificateCheck https://ledger.localhost:7443/health).Headers["X-Upstream-Addr"]
}
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Se todas as chamadas aparecerem em apenas um upstream durante baixa concorrencia, repita com mais chamadas ou concorrencia. `least_conn` escolhe a instancia com menos conexoes ativas; em chamadas muito curtas, a distribuicao pode parecer agrupada em uma amostra pequena.

## X-Correlation-Id via Nginx nao aparece ou nao bate

Quando a chamada passa pela borda local, o Nginx deve devolver `X-Correlation-Id`, encaminhar o mesmo valor para a API e registrar `correlation_id` no access log JSON.

Valide uma chamada sem header explicito:

```bash
curl -k -i https://ledger.localhost:7443/health
```

Valide preservacao de valor enviado pelo cliente:

```bash
curl -k -i -H "X-Correlation-Id: 11111111-1111-4111-8111-111111111111" https://ledger.localhost:7443/health
```

Confira os logs:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Cada linha de access log deve ser JSON valido e conter `correlation_id`. Se o valor enviado pelo cliente nao for um UUID valido, o Nginx ainda o encaminha, mas o middleware da API pode normalizar e devolver outro UUID.

## APIs nao reconhecem https ou host via Nginx

As APIs usam `UseForwardedHeaders` antes de Swagger, redirecionamento HTTPS, autenticacao e autorizacao para aplicar `X-Forwarded-For`, `X-Forwarded-Proto` e `X-Forwarded-Host`. Se algum link aparecer como HTTP ou com host interno:

- confirme que a chamada entrou por `https://ledger.localhost:7443`, `https://balance.localhost:7443` ou `https://auth.localhost:7443`;
- confira se o Nginx esta enviando `X-Forwarded-Proto https` e `X-Forwarded-Host $host`;
- recrie as imagens das APIs depois de alterar codigo C#: `docker compose -f compose.yaml -f compose.nginx.yaml up -d --build`;
- valide a configuracao efetiva: `docker compose -f compose.yaml -f compose.nginx.yaml config`.

## Readiness retorna 503

`GET /ready` nas APIs valida dependencias obrigatorias para trafego HTTP. Em geral, investigue:

- conexao com PostgreSQL;
- connection strings usadas no ambiente.

Kafka, topicos, `Kafka:Enabled`, bootstrap servers e DLQ pertencem aos workers. Quando houver falha de consumo ou publicacao, investigue logs e metricas de `LedgerService.Worker` e `BalanceService.Worker`; a indisponibilidade do Kafka consumer nao deve derrubar o readiness do `BalanceService.Api`.

No compose local, use `docker compose logs -f ledger-worker` para Outbox/estornos/reprocessamentos e `docker compose logs -f balance-worker` para consumo Kafka/DLQ. `ledger-service` e `balance-service` representam apenas as APIs HTTP.

Detalhes ficam em [observabilidade](observability.md#readiness) e [Kafka, Outbox e DLQ](development/kafka-outbox.md).

## password authentication failed for user "userBalance"

Esse erro indica que o `BalanceService.Api`, `BalanceService.Worker`, migration ou runner k6 tentou acessar o PostgreSQL do Balance com as credenciais locais configuradas, mas o banco recusou a autenticacao.

A causa mais comum e um volume `balance-postgres-data` inicializado com uma senha antiga. Em containers PostgreSQL, `POSTGRES_USER`, `POSTGRES_PASSWORD` e `POSTGRES_DB` so criam/alteram credenciais quando o diretorio de dados esta vazio. Alterar `.env` ou `compose.yaml` depois que o volume existe nao troca a senha dentro do banco.

Confirme nos logs:

```bash
docker compose logs balance-db
docker compose logs balance-service
```

Valide a autenticacao real com as credenciais configuradas. Se voce usa os defaults locais:

```bash
docker compose exec -T -e PGPASSWORD=local_dev_password balance-db psql -h balance-db -U userBalance -d dbBalance -c "select 1;"
```

Se houver `.env`, confira se `BALANCE_DB_NAME`, `BALANCE_DB_USER` e `BALANCE_DB_PASSWORD` batem com a connection string efetiva do compose:

```bash
docker compose config
```

Quando a senha antiga for conhecida, atualize a senha manualmente dentro do PostgreSQL. Exemplo usando o usuario configurado do Balance:

```bash
docker compose exec -T -e PGPASSWORD=<senha-antiga> balance-db psql -h balance-db -U userBalance -d dbBalance -c "ALTER USER \"userBalance\" WITH PASSWORD 'local_dev_password';"
```

Se os dados locais forem descartaveis, recrie conscientemente apenas o volume do Balance. Esta acao apaga dados locais desse banco:

```bash
docker compose stop balance-service balance-worker balance-db
docker compose rm -f balance-db
docker volume ls
docker volume rm poc-arquitetura_balance-postgres-data
docker compose up -d balance-db
docker compose exec -T -e PGPASSWORD=local_dev_password balance-db psql -h balance-db -U userBalance -d dbBalance -c "select 1;"
```

Use o nome real mostrado por `docker volume ls` caso o projeto Compose tenha outro nome. Nao use `docker compose down -v`, porque isso remove volumes de outros servicos. Depois de recriar o banco, aplique as migrations pelo fluxo local documentado e reexecute o smoke de carga:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/start-local-stack.ps1 -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-loadtests.ps1 -Mode smoke
```

Nenhum script do repositorio remove volumes automaticamente.

## Outbox fica em Pending ou DeadLetter

Mensagens `Pending` podem ser normais durante a janela de polling. Se permanecerem acumuladas ou chegarem a `DeadLetter`, investigue Kafka, topic map, ACL/configuracao local, serializacao e `last_error`.

O publisher roda no `LedgerService.Worker`; se o `LedgerService.Api` estiver saudavel mas a Outbox nao avancar, valide primeiro se o container/processo `ledger-worker` esta ativo e com `ServiceName=LedgerService.Worker`.

Use [Kafka, Outbox e DLQ](development/kafka-outbox.md#outbox) para entender estados, backoff e requeue operacional. Nao use requeue para mascarar erro permanente de contrato ou payload.

## Balance nao atualiza saldo

Confirme a cadeia completa:

1. lancamento criado no Ledger;
2. mensagem em `outbox_messages`;
3. `ledger-worker` publicou a mensagem e ela chegou ao status final `Processed`;
4. `balance-worker` consumiu o evento e registrou `processed_events`;
5. `balance-worker` atualizou `daily_balances`;
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

Esses componentes ficam no profile `observability` e nao sobem na stack minima. Confirme se a stack local foi iniciada com observabilidade e se as portas estao livres:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

Ou diretamente pelo compose:

```bash
OTEL_ENABLED=true docker compose --profile observability up -d --build
```

- Jaeger UI: `http://localhost:16686/`
- Prometheus: `http://localhost:9090/`
- Loki: `http://localhost:3100/`
- Alertmanager: `http://localhost:9093/`
- Grafana: `http://localhost:3000/`

O desenho da stack e as validacoes ficam em [observabilidade](observability.md#configuracao-local).

Para investigar a partir de um dashboard:

1. Abra `http://localhost:3000`.
2. Acesse a pasta `Observability`.
3. Abra `APIs - Visao Geral` ou `Runtime .NET - Visao Geral`.
4. Ajuste o periodo e clique em `Logs no Loki`.
5. No Explore, pesquise `CorrelationId=<valor>` ou `TraceId=<valor>`.

Quando o log contem `TraceId=<valor>`, o datasource Loki mostra o link `Abrir trace no Jaeger`. Use `TraceId` para navegar para a arvore de spans no Jaeger e use `CorrelationId` para conectar logs, responses HTTP, Outbox, Kafka e consultas SQL do fluxo funcional.

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
