# Troubleshooting

Este guia aponta os caminhos de diagnostico mais comuns sem duplicar os guias operacionais completos.

## Migrations falham

Confirme se o PostgreSQL local esta acessivel e se a connection string usa a porta correta do compose:

- PostgreSQL: `localhost:15432`

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
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Detalhes ficam em [Testcontainers e Docker-compatible API](development/local-development.md#testcontainers-e-docker-compatible-api).

## Swagger nao abre

Swagger/OpenAPI fica habilitado por padrao somente em `Development`. Fora desse ambiente, a exposicao exige `Swagger:Enabled=true`.

Confirme tambem se a API correta esta rodando:

- LedgerService.Api: `http://localhost:5226/`
- BalanceService.Api: `http://localhost:5228/`

Quando estiver usando a borda local com Nginx, confirme tambem:

- o certificado local existe em `infra/nginx/certs/localhost.crt` e `infra/nginx/certs/localhost.key`;
- o certificado possui SAN para `localhost`, `ledger.localhost` e `balance.localhost`;
- o overlay foi aplicado no comando: `docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge`;
- os logs do Nginx nao mostram erro de certificado ou upstream: `docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge`;
- as URLs via proxy usam subdominio, nao path: `https://ledger.localhost:7443/swagger` e `https://balance.localhost:7443/swagger`.

Veja [Swagger e endpoints operacionais](development/local-development.md#swagger-e-endpoints-operacionais).

## Nginx local nao inicia

O Nginx local e opcional e depende dos arquivos de certificado montados por volume. Se o container `poc-nginx-edge` sair imediatamente, valide a configuracao efetiva:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml config
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Erros comuns:

- `cannot load certificate`: gere `infra/nginx/certs/localhost.crt` com `./scripts/local/generate-certs.ps1` ou `./scripts/local/generate-certs.sh`;
- `cannot load certificate key`: gere `infra/nginx/certs/localhost.key` com `./scripts/local/generate-certs.ps1` ou `./scripts/local/generate-certs.sh`;
- alerta de certificado no navegador: confie o certificado local ou use `mkcert -install`;
- `connection refused` ao abrir Swagger via Nginx: confirme se `ledger-service-1`, `ledger-service-2` e `balance-service` estao em execucao e saudaveis.

O Nginx nao altera as portas HTTP diretas. Se precisar isolar o problema, valide primeiro a Swagger UI direta em `http://localhost:5226/index.html` e `http://localhost:5228/index.html`, ou os documentos OpenAPI em `/swagger/v1/swagger.json`.

## Stack completa nao sobe por recurso em uso

Se `./scripts/local/start-full-stack.ps1` ou `./scripts/local/start-full-stack.sh` encontrar containers antigos do overlay Nginx, rede local presa ou portas ocupadas, ele para antes de subir a stack completa e informa o recurso afetado.

Quando o recurso pertence ao proprio projeto, o script pergunta se pode executar uma limpeza nao destrutiva equivalente a:

```bash
docker compose -f compose.yaml -f compose.observability.yaml -f compose.nginx.yaml --profile observability --profile direct-ledger down --remove-orphans
```

Esse comando nao usa `-v`: ele para/remove containers e redes locais do projeto, mas preserva volumes, bancos locais, imagens e certificados. Para autorizar essa limpeza sem prompt:

```powershell
./scripts/local/start-full-stack.ps1 -Cleanup
```

```bash
./scripts/local/start-full-stack.sh --cleanup
```

Se a porta estiver ocupada por processo externo ou container que nao pertence ao projeto, libere manualmente o processo/container indicado antes de executar o script de novo. A limpeza automatica nao para recursos externos ao projeto.

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
```

Se os headers nao aparecerem, recrie o container com o overlay e confirme que `infra/nginx/security-headers.conf` foi montado:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
```

## Nginx expoe Server ou headers de tecnologia

A borda local deve reduzir fingerprinting sem alterar contratos HTTP de negocio. As respostas via Nginx nao devem conter `Server` nem repassar headers como `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` e `X-Swagger-UI-Version` das APIs internas. Para isso, o overlay usa uma imagem local com Nginx e o modulo `headers-more`.

Valide:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
```

Se aparecer `Server`, `X-Powered-By` ou `X-Swagger-UI-Version`, recrie a imagem/container com o overlay e valide a configuracao carregada:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
```

## Cache-Control nao aparece via Nginx local

Os hosts de API via Nginx devem devolver `Cache-Control: no-store`, `Pragma: no-cache` e `Expires: 0` para evitar cache indevido de respostas sensiveis. Essa politica se aplica a `ledger.localhost` e `balance.localhost`; o portal estatico em `https://localhost:7443` fica fora da regra.

Valide:

```bash
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://ledger.localhost:7443/health
```

Se os headers nao aparecerem, recrie o container com o overlay e valide a configuracao carregada:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
```

## Nginx retorna 413 ou 429

`413 Payload Too Large` indica que o body ultrapassou `client_max_body_size 1m` na borda local. Esse limite fica alinhado ao limite padrao das APIs Ledger e Balance e rejeita a chamada antes de chegar ao ASP.NET.

`429 Too Many Requests` indica excesso de requisicoes ou conexoes por IP na borda local. O overlay usa `limit_req` de 10 requisicoes por segundo com `burst=40` e `limit_conn` de 20 conexoes simultaneas por IP. Isso protege a POC contra bursts acidentais sem representar uma politica real de producao.

Valide a configuracao carregada:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge nginx -t
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Para diferenciar bloqueio na borda de erro da API, confira os logs JSON do Nginx. Em bloqueios de payload, rate limit ou conexao, o campo `status` mostra `413` ou `429` e `upstream_status` tende a ficar vazio, pois a requisicao nao foi encaminhada ao upstream.

Se Swagger, portal ou chamadas normais receberem `429`, reduza concorrencia local, aguarde alguns segundos para a janela esvaziar e repita. Se um teste de carga precisar medir as APIs sem a borda, use os runners k6 existentes, que continuam apontando para as portas HTTP diretas do compose. Se a intencao for medir a borda, considere criar um cenario k6 separado e aceite que `429` faca parte do comportamento esperado do proxy.

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

- confirme que a chamada entrou por `https://ledger.localhost:7443` ou `https://balance.localhost:7443`;
- confira se o Nginx esta enviando `X-Forwarded-Proto https` e `X-Forwarded-Host $host`;
- recrie as imagens das APIs depois de alterar codigo C#: `docker compose -f compose.yaml -f compose.nginx.yaml up -d --build`;
- valide a configuracao efetiva: `docker compose -f compose.yaml -f compose.nginx.yaml config`.

## Readiness retorna 503

`GET /ready` nas APIs valida dependencias obrigatorias para trafego HTTP. Em geral, investigue:

- conexao com PostgreSQL;
- connection strings usadas no ambiente.

Kafka, Pub/Sub legado, topics, subscriptions, flags de provider e DLQ pertencem aos workers. Quando houver falha de consumo ou publicacao, investigue logs e metricas de `LedgerService.Worker` e `BalanceService.Worker`; a indisponibilidade do consumer nao deve derrubar o readiness do `BalanceService.Api`.

No compose local, use `docker compose -f compose.yaml logs -f ledger-worker balance-worker kafka kafka-init-topics` para o fluxo principal. No modo Pub/Sub legado, use `docker compose -f compose.yaml -f compose.pubsub.yaml --profile legacy-pubsub logs -f ledger-worker balance-worker pubsub-emulator pubsub-init`. `ledger-service` e `balance-service` representam apenas as APIs HTTP.

Detalhes ficam em [observabilidade](observability.md#readiness) e [Kafka, Outbox e DLQ](development/kafka-outbox.md).

## Webhook Stripe local falha

O endpoint canonico do PaymentService e:

```text
POST http://localhost:5234/api/v1/webhooks/stripe
```

Use `stripe listen` em modo manual:

```bash
stripe listen --forward-to http://localhost:5234/api/v1/webhooks/stripe
```

Erros comuns:

| Sintoma | Causa provavel | Acao |
| --- | --- | --- |
| `stripe: command not found` | Stripe CLI ausente ou fora do `PATH`. | Instale pelo metodo oficial e valide com `stripe version`. |
| `Webhook Stripe nao configurado` | `PaymentGateway:Stripe:WebhookSigningSecret` vazio no processo da API. | Copie o `whsec_...` impresso pelo `stripe listen` para user secrets ou variavel local. |
| `No signatures found matching the expected signature for payload` | Secret errado, endpoint incorreto, body alterado, header ausente, proxy alterando payload ou variavel nao carregada. | Confirme `whsec_...`, URL `http://localhost:5234/api/v1/webhooks/stripe`, raw body e ambiente do processo. |
| Evento chega, mas Payment nao muda | Evento sintetico sem `metadata.payment_id`, Payment local inexistente, Worker parado, retry ou DeadLetter. | Diferencie smoke sintetico de fluxo correlacionado e confira `payment.inbox_messages`. |
| Evento chega, mas Ledger nao recebe | Evento nao corresponde a Payment real ou Payment ainda nao chegou ao estado adequado; Worker/integacao Ledger pode estar em retry. | Verifique Payment, Worker, estado de integracao Ledger, logs e metricas. |
| Payment fica `Succeeded` ou `LedgerPending` | `PaymentService.Worker` nao conseguiu criar o CREDIT no Ledger. | Verifique `LedgerService.Api`, token service-to-service `ledger.write`, retry persistido em `payment.payments` e logs com `X-Correlation-Id`. |
| Refund solicitado, mas Payment nao vira `Refunded` | Webhook `refund.updated` ausente, Worker parado ou estorno no Ledger pendente. | Confira `payment.payment_refunds`, `payment.inbox_messages`, `ledger_reversal_status`, retry e DeadLetter. |
| Balance nao atualiza apos Payment/Refund | Payment nao chama Balance diretamente; o saldo depende do Ledger Outbox -> Kafka -> Balance Worker. | Verifique Outbox do Ledger, `ledger-worker`, Kafka, `balance-worker`, DLQ e tabela `balance.processed_events`. |
| Smoke local falha com assinatura Stripe | `PAYMENT_WEBHOOK_SIGNING_SECRET` diferente de `PaymentGateway:Stripe:WebhookSigningSecret` ou raw body alterado. | Use o mesmo `whsec` local nos dois lados e nao passe por proxy que modifique o corpo. |
| `stripe trigger` nao suporta determinado evento | A CLI nao possui fixture para o evento desejado. | Use evento suportado ou fluxo correlacionado real no sandbox. |

Detalhes ficam em [validacao local de webhooks Stripe com Stripe CLI](development/stripe-cli-webhooks.md).

## password authentication failed for user "balance_write_user"

Esse erro indica que uma API, worker, migration ou runner k6 tentou acessar o PostgreSQL local com as credenciais configuradas, mas o banco recusou a autenticacao.

A causa mais comum e um volume `postgres-data` inicializado antes da mudanca de senha, role ou grant. Em containers PostgreSQL, scripts em `/docker-entrypoint-initdb.d` e variaveis `POSTGRES_*` so criam/alteram objetos quando o diretorio de dados esta vazio. Alterar `.env` ou `compose.yaml` depois que o volume existe nao troca a senha dentro do banco.

Confirme nos logs:

```bash
docker compose logs postgres-db
docker compose logs balance-service
```

Valide a autenticacao real com as credenciais configuradas. Se voce usa os defaults locais:

```bash
docker compose --env-file .env.local exec -T -e PGPASSWORD=<BALANCE_DB_WRITE_PASSWORD> postgres-db psql -h postgres-db -U balance_write_user -d appdb -c "select 1;"
```

Se houver `.env`, confira se `LEDGER_DB_PASSWORD`, `BALANCE_DB_READ_PASSWORD`, `BALANCE_DB_WRITE_PASSWORD` e `BALANCE_DB_MIGRATOR_PASSWORD` batem com a connection string efetiva do compose:

```bash
docker compose config
```

Quando a senha administrativa local for conhecida, atualize a senha manualmente dentro do PostgreSQL. Exemplo para o usuario de escrita do Balance:

```bash
docker compose --env-file .env.local exec -T -e PGPASSWORD=<POSTGRES_PASSWORD> postgres-db psql -h postgres-db -U postgres_admin -d appdb -c "ALTER USER balance_write_user WITH PASSWORD '<BALANCE_DB_WRITE_PASSWORD>';"
```

Se os dados locais forem descartaveis, recrie conscientemente o volume PostgreSQL. Esta acao apaga dados locais dos schemas `ledger` e `balance`:

```bash
docker compose stop ledger-service ledger-worker balance-service balance-worker postgres-db
docker compose rm -f postgres-db
docker volume ls
docker volume rm poc-arquitetura_postgres-data
docker compose up -d postgres-db
docker compose --env-file .env.local exec -T -e PGPASSWORD=<BALANCE_DB_WRITE_PASSWORD> postgres-db psql -h postgres-db -U balance_write_user -d appdb -c "select 1;"
```

Use o nome real mostrado por `docker volume ls` caso o projeto Compose tenha outro nome. Nao use `docker compose down -v`, porque isso remove volumes de outros servicos. Depois de recriar o banco, aplique as migrations pelo fluxo local documentado e reexecute o smoke de carga:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/local/start-stack.ps1 -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/performance/run-loadtests.ps1 -Mode smoke-kafka
```

Nenhum script do repositorio remove volumes automaticamente.

## Outbox fica em Pending ou DeadLetter

Mensagens `Pending` podem ser normais durante a janela de polling. Se permanecerem acumuladas ou chegarem a `DeadLetter`, investigue o provider selecionado, topic map, IAM/ACL ou configuracao local, serializacao e `last_error`.

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

O roteiro operacional completo fica em [validacao Keycloak -> Ledger -> Outbox -> Kafka -> Balance](observability.md#validacao-keycloak---ledger---outbox---kafka---balance).

## Token JWT e rejeitado

Confirme issuer, audience, scopes e merchant:

- `scope` deve conter o scope exigido pelo endpoint;
- `merchant_id` deve conter o merchant usado no body ou query;
- `aud` deve conter a audience da API;
- fora de `Development` e `Local`, JWKS deve usar HTTPS.

Veja [autenticacao e autorizacao](development/authentication.md).

## Grafana, Prometheus, Loki ou Jaeger nao mostram dados

Esses componentes ficam no overlay `compose.observability.yaml` com profile `observability` e nao sobem no core funcional. Confirme se a stack local foi iniciada com observabilidade e se as portas estao livres:

```powershell
./scripts/local/start-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/local/start-stack.sh
```

Ou diretamente pelo compose:

```bash
OTEL_ENABLED=true docker compose -f compose.yaml -f compose.observability.yaml --profile observability up -d --build
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

Quando o log contem `TraceId=<valor>`, o datasource Loki mostra o link `Abrir trace no Jaeger`. Use `TraceId` para navegar para a arvore de spans no Jaeger e use `CorrelationId` para conectar logs, responses HTTP, Outbox, mensagens do provider selecionado e consultas SQL do fluxo funcional.

## Load tests falham

Os testes k6 rodam em container dentro da rede do compose e exigem a stack local ativa. Comece pelo modo smoke:

```powershell
./scripts/performance/run-loadtests.ps1 -Mode smoke-kafka
```

No Linux/macOS:

```bash
./scripts/performance/run-loadtests.sh smoke-kafka
```

Detalhes ficam em [load tests com k6](development/local-development.md#load-tests-com-k6) e [loadtests/k6](../loadtests/k6/README.md).

## OWASP ZAP local falha

Os scripts ZAP exigem Docker, `docker compose` e a stack da POC ja iniciada. Antes do scan, eles validam `GET /health` em Ledger e Balance. Se a falha mencionar uma URL direta, suba o core funcional:

```powershell
./scripts/local/start-stack.ps1
```

```bash
./scripts/local/start-stack.sh
```

Ou deixe o proprio runner ZAP chamar esse fluxo explicitamente:

```powershell
./scripts/security/run-owasp-zap.ps1 -StartStack
```

```bash
./scripts/security/run-owasp-zap.sh --start-stack
```

Se a falha mencionar `https://*.localhost:7443`, suba a stack completa com Nginx e confirme os certificados locais:

```powershell
./scripts/local/start-full-stack.ps1
./scripts/security/run-owasp-zap.ps1 -UseNginx
```

```bash
./scripts/local/start-full-stack.sh
./scripts/security/run-owasp-zap.sh --use-nginx
```

O ZAP roda em container e acessa o host por `host.docker.internal` ou por hosts `.localhost` mapeados para `host-gateway`. Se o Docker local nao suportar `host-gateway`, use URLs acessiveis a partir de containers e sobrescreva os alvos com `-AuthUrl`, `-LedgerUrl`, `-BalanceUrl` ou `--auth-url`, `--ledger-url`, `--balance-url`.

Depois do health check, o runner importa `/swagger/v1/swagger.json` de cada API usando `zap-api-scan.py -f openapi`. Se o erro mencionar importacao OpenAPI ou documento Swagger ausente, confirme se a API esta em `Development` ou com Swagger habilitado e valide manualmente:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5226/swagger/v1/swagger.json
```

```bash
curl -fsS http://localhost:5226/swagger/v1/swagger.json
```

Os scripts removem apenas o container temporario `poc-arquitetura-zap`. Se uma execucao for interrompida, remova manualmente esse container sem derrubar a stack principal:

```bash
docker rm -f poc-arquitetura-zap
```

Detalhes ficam em [OWASP ZAP local](development/owasp-zap.md).
