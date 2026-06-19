# Desenvolvimento local

Este guia concentra os passos para executar, validar e depurar a POC localmente.

## Pre-requisitos

Para a stack local:

- Docker-compatible API acessivel;
- CLI `docker` com suporte a `docker compose`;
- build de imagens habilitado no runtime local.

O projeto nao exige Docker Desktop como premissa. No Windows sem Docker Desktop, o ambiente recomendado e Rancher Desktop usando `moby/dockerd`, pois ele expoe uma API compativel com Docker. `containerd` puro com `nerdctl` nao deve ser tratado como ambiente suportado para os testes baseados em Testcontainers.

Para rodar no host:

- .NET SDK definido em `global.json`;
- PostgreSQL e Kafka acessiveis localmente; Pub/Sub emulator e necessario apenas para o modo explicito/legado;
- ferramentas locais restauradas com `dotnet tool restore`.

Ferramentas opcionais:

- `curl`, para exemplos HTTP;
- VS Code, para workspace, tasks e REST Client;
- Node.js LTS, para gerar a documentacao LikeC4 localmente.

Detalhes sobre Node.js, npm, npx, tools .NET locais e validacoes OpenAPI/LikeC4 ficam em [ferramentas auxiliares](tooling.md).

## Escopo local e credenciais

Esta stack e local, descartavel e nao deve ser promovida para ambientes compartilhados, homologacao ou producao sem revisao de seguranca, secrets, transporte, imagens e observabilidade.

O `compose.yaml` atual e a base oficial do ambiente local. O `compose.debug.yml` inclui essa mesma base para oferecer um ponto de entrada curto para dependencias de debug. Reaproveite os servicos ja existentes, em especial `postgres-db`, `kafka` e `kafka-init-topics`; nao crie um segundo Compose nem um segundo PostgreSQL para separar Ledger, Balance e Transfer no desenvolvimento local. Pub/Sub emulator permanece no compose base apenas como profile explicito/legado.

O compose nao versiona senhas locais. Para subir a stack por comandos diretos, crie `.env.local` a partir de `.env.local.example` e preencha os placeholders na sua maquina:

```bash
cp .env.local.example .env.local
```

No PowerShell:

```powershell
Copy-Item .env.local.example .env.local
```

O caminho mais rapido para onboarding e gerar valores locais descartaveis:

```powershell
./scripts/init-env-local.ps1
```

No Linux/macOS:

```bash
./scripts/init-env-local.sh
```

O script nao sobrescreve `.env.local` existente; use `-Force` no PowerShell ou `--force` no shell apenas quando quiser recriar conscientemente o arquivo local. O arquivo `.env.local` e ignorado pelo Git e nao deve ser versionado. Os scripts `start-local-stack.*` e `start-full-stack.*` tambem leem `.env.local` automaticamente; `.env` permanece aceito como fallback para compatibilidade com fluxos antigos.

Comandos manuais de Compose devem usar `--env-file .env.local`, porque o Docker Compose nao carrega `.env.local` automaticamente. O comando sem `--env-file`, como `docker compose -f compose.yaml config --quiet`, carrega apenas variaveis exportadas no ambiente da sessao e o arquivo `.env` da raiz do repositorio. Se voce precisa usar esse formato curto, crie um `.env` local ignorado pelo Git a partir do exemplo versionado:

```bash
cp .env.example .env
```

No PowerShell:

```powershell
Copy-Item .env.example .env
```

Depois preencha os placeholders no `.env`. Nao copie valores reais para `.env.example` ou `.env.local.example`.

Variaveis sensiveis obrigatorias para o compose principal:

- `POSTGRES_PASSWORD`
- `LEDGER_DB_PASSWORD`
- `LEDGER_DB_MIGRATOR_PASSWORD`
- `BALANCE_DB_READ_PASSWORD`
- `BALANCE_DB_WRITE_PASSWORD`
- `BALANCE_DB_MIGRATOR_PASSWORD`
- `TRANSFER_DB_PASSWORD`
- `TRANSFER_DB_MIGRATOR_PASSWORD`
- `KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD`
- `KEYCLOAK_CLIENT_SECRET`
- `KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD`
- `KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD`
- `KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD`

Variaveis nao sensiveis ou identificadores locais continuam com defaults no compose ou exemplos em `.env.local.example`, como `POSTGRES_HOST_PORT=15432`, `PUBSUB_EMULATOR_HOST_PORT=8085`, `PUBSUB_PROJECT_ID=poc-local` e os nomes locais de topics/subscriptions: `PUBSUB_LEDGER_EVENTS_TOPIC_ID`, `PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID`, `PUBSUB_BALANCE_SUBSCRIPTION_ID` e `PUBSUB_LEDGER_EVENTS_DLQ_INSPECTION_SUBSCRIPTION_ID`.

As variaveis `AUTH_POC_USERNAME`, `AUTH_POC_PASSWORD` e `AUTH_POC_SCOPE` continuam aceitas apenas pelo overlay legado `compose.auth-legacy.yaml`.

O mesmo `KEYCLOAK_CLIENT_SECRET` alimenta o client local `poc-automation` usado pelos scripts e pelo `TransferService.Worker` quando ele chama o `LedgerService.Api`. O scope service-to-service do worker fica em `TRANSFER_WORKER_LEDGER_AUTH_SCOPE`, com default `ledger.write`.

O PostgreSQL local roda em um unico container `postgres-db`, com volume `postgres-data`, database `appdb`, schemas `ledger`, `balance` e `transfer`, e usuarios separados por servico/responsabilidade. A inicializacao fica nos scripts versionados em `infra/postgres/init`. As connection strings dos servicos runtime no compose usam `postgres-db:5432/appdb`; as variaveis `LEDGER_DB_*`, `BALANCE_DB_*` e `TRANSFER_DB_*` configuram as senhas locais usadas pelo init do container e pelas connection strings do compose. Em volumes PostgreSQL existentes, alterar `.env.local` ou `compose.yaml` nao altera automaticamente roles, grants ou senhas ja gravadas no banco; para reaplicar o init, recrie conscientemente o volume local ou execute o SQL manualmente.

Topologia local de banco:

| Responsabilidade | Database/schema | Usuario |
| --- | --- | --- |
| Runtime do LedgerService.Api e LedgerService.Worker | `appdb.ledger` | `ledger_app_user` |
| Migrations do LedgerService | `appdb.ledger` | `ledger_migrator_user` |
| Runtime de leitura do BalanceService.Api | `appdb.balance` | `balance_read_user` |
| Runtime de escrita do BalanceService.Worker | `appdb.balance` | `balance_write_user` |
| Migrations do BalanceService | `appdb.balance` | `balance_migrator_user` |
| Runtime do TransferService.Api e TransferService.Worker | `appdb.transfer` | `transfer_app_user` |
| Migrations do TransferService | `appdb.transfer` | `transfer_migrator_user` |

O `BalanceService.Api` deve permanecer read-only no banco: ele consulta a projecao `balance`, mas nao aplica eventos nem executa INSERT/UPDATE/DELETE. A escrita da projecao pertence ao `BalanceService.Worker` com `balance_write_user`. A reducao para um unico container e apenas local; o isolamento logico entre servicos continua sendo preservado por schemas, migrations separadas e grants por role.

Nao reutilize esses valores fora da maquina local. Em ambientes compartilhados ou produtivos, use um mecanismo proprio de secret/config store e credenciais rotacionaveis.

## Stack local com compose

Os scripts `start-local-stack.*` sobem por padrao o core funcional de desenvolvimento com `compose.yaml`, que deve continuar sendo reaproveitado como fonte de verdade do ambiente local:

- Keycloak;
- `LedgerService.Api`;
- `LedgerService.Worker`;
- `BalanceService.Api`;
- `BalanceService.Worker`;
- `TransferService.Api`;
- `TransferService.Worker`;
- PostgreSQL unico (`postgres-db`) com schemas `ledger`, `balance` e `transfer`;
- Kafka local;
- job idempotente de inicializacao dos topicos Kafka de Ledger, Balance e Transfer.

Esse modo tambem pode ser chamado de Dev Lite quando o foco for reduzir consumo local: ele significa core funcional sem observabilidade, SonarQube, k6, Nginx overlay e `Auth.Api` legado. Dev Lite nao significa "sem workers"; `LedgerService.Worker`, `BalanceService.Worker` e `TransferService.Worker` continuam no fluxo padrao porque validam Outbox, Kafka, projecao e processamento de Saga ponta a ponta.

Componentes opcionais ficam em arquivos Compose separados. Eles complementam o `compose.yaml`; nao substituem a stack principal nem duplicam banco, Kafka ou servicos .NET:

- `compose.observability.yaml` com profile `observability`: OpenTelemetry Collector, Jaeger, Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana;
- `compose.k6.yaml` com profile `k6`: container k6;
- `compose.nginx.yaml`: borda local Nginx opcional;
- `compose.sonar.yaml` com profile `quality`: SonarQube local;
- `compose.auth-legacy.yaml` com profile `legacy-auth`: `Auth.Api` legado.
- `compose.yaml`: stack principal com Kafka, init idempotente dos topicos e workers configurados com `Messaging:Provider=Kafka`.
- `compose.pubsub.yaml` com profile `legacy-pubsub`: caminho explicito/legado para Pub/Sub emulator, `pubsub-init` e overrides dos workers de Ledger/Balance para `Messaging:Provider=PubSub`.
- `compose.kafka.yaml`: overlay mantido apenas como alias compativel; Kafka ja faz parte do compose principal.
- `compose.cloudsql.yaml`: overlay de smoke manual/local com Cloud SQL Auth Proxy, trocando `postgres-db:5432` por `cloud-sql-proxy:5432` dentro dos containers.

Tambem existe um overlay opcional `compose.nginx.yaml` para adicionar uma borda local com Nginx e HTTPS em desenvolvimento. Ele nao faz parte do core funcional e nao altera as APIs, que continuam rodando internamente em HTTP com `ASPNETCORE_URLS=http://+:8080`. Quando o overlay e usado, o Nginx cria um upstream local `ledger_api` com duas instancias da `LedgerService.Api` e algoritmo `least_conn`. O `Auth.Api` foi removido do core funcional e permanece apenas no overlay legado `compose.auth-legacy.yaml`.

A observabilidade completa inclui:

- OpenTelemetry Collector como entrada local de telemetria OTLP;
- Jaeger all-in-one como backend local de visualizacao de traces;
- Prometheus para coletar metricas tecnicas expostas pelo Collector;
- Loki para armazenar logs centralizados dos containers;
- Grafana Alloy para coletar logs dos containers via Docker API, isolado no profile `observability`;
- Alertmanager local para visualizar alertas tecnicos basicos sem envio externo;
- Grafana com datasources Prometheus, Loki e Jaeger e dashboards minimos provisionados.

Subir o core funcional com migrations:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse fluxo sobe banco, Kafka e Keycloak, aplica migrations pelo host e depois inicia `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api`, `BalanceService.Worker`, `TransferService.Api` e `TransferService.Worker`.

Os scripts `start-local-stack.*` usam Docker Compose, restauram tools .NET, aplicam migrations pelo host e nao removem volumes. Eles nao executam testes automatizados, k6 nem scanners.

Para subir tambem observabilidade e habilitar exportacao OTLP nas aplicacoes:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

Para subir somente o core funcional pelo compose, sem aplicar migrations:

```bash
docker compose --env-file .env.local -f compose.yaml up -d --build
```

Para subir somente dependencias externas usadas por processos em debug no host, reaproveite os servicos do `compose.yaml`:

```bash
docker compose --env-file .env.local -f compose.yaml up -d postgres-db kafka kafka-init-topics keycloak
```

O alias equivalente para debug e:

```powershell
docker compose --env-file .env.local -f compose.debug.yml up -d postgres-db kafka kafka-init-topics keycloak
```

Para subir os processos .NET pelo compose depois das dependencias:

```bash
docker compose --env-file .env.local -f compose.yaml up -d ledger-service ledger-worker balance-service balance-worker transfer-service transfer-worker
```

Para verificar o status dos containers do core local:

```bash
docker compose --env-file .env.local -f compose.yaml ps
```

Para consultar logs das dependencias locais:

```bash
docker compose --env-file .env.local -f compose.yaml logs postgres-db kafka kafka-init-topics
```

Para derrubar os containers e redes do core local, preservando volumes:

```bash
docker compose --env-file .env.local -f compose.yaml down
```

Esse comando inicia apenas o core funcional. Para habilitar observabilidade completa pelo compose, incluindo coleta local de logs via Docker API, use:

```bash
OTEL_ENABLED=true docker compose --env-file .env.local -f compose.yaml -f compose.observability.yaml --profile observability up -d --build
```

`OTEL_ENABLED=true` habilita as aplicacoes a exportarem traces e metricas para `otel-collector:4317`. Sem essa variavel, os backends de observabilidade podem subir, mas as aplicacoes permanecem com OpenTelemetry desabilitado para manter o core funcional leve.

Para smoke manual contra Cloud SQL via Auth Proxy, use o overlay dedicado:

```bash
docker compose -f compose.yaml -f compose.cloudsql.yaml up -d cloud-sql-proxy
docker compose -f compose.yaml -f compose.cloudsql.yaml up -d --build
```

Nesse modo, os containers das APIs e workers usam
`Host=cloud-sql-proxy;Port=5432`. O host, quando precisar acessar o mesmo
proxy com `psql` ou outra ferramenta, usa `127.0.0.1:${CLOUDSQL_PROXY_HOST_PORT:-5432}`.
Nao use `localhost` dentro dos containers para acessar o proxy.

Detalhes de credenciais, variaveis e validacao ficam em
[Cloud SQL PostgreSQL local com Auth Proxy](cloudsql-postgres-local-setup.md).

### Pub/Sub emulator local

Pub/Sub permanece disponivel como provider explicito/legado. Use este modo quando quiser validar o adapter Pub/Sub, o emulator local ou cenarios GCP relacionados. Os aliases abaixo deixam essa escolha explicita:

```powershell
./scripts/start-local-stack-pubsub.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack-pubsub.sh
```

Os scripts reaproveitam o fluxo de migrations e APIs, mas trocam a mensageria de Ledger/Balance para Pub/Sub e nao sobem Kafka nem `TransferService.Worker`. O overlay adiciona:

- `pubsub-emulator`, exposto no host em `localhost:8085` por padrao;
- `pubsub-init`, que cria idempotentemente o topic principal, o topic de DLQ, a subscription pull do `BalanceService.Worker` e a subscription de inspecao da DLQ de aplicacao;
- `PUBSUB_EMULATOR_HOST=pubsub-emulator:8085` e `PUBSUB_PROJECT_ID` nos workers que usam Pub/Sub;
- overrides `PubSub__Producer__*`, `PubSub__Consumer__*` e `Messaging__Provider=PubSub`.

Os nomes locais seguem a configuracao de `.env.local.example` e podem ser sobrescritos por `.env.local`:

| Recurso | Default local |
| --- | --- |
| Projeto do emulator | `poc-local` |
| Topic principal | `ledger.ledgerentry.created.local` |
| Topic de DLQ | `ledger.ledgerentry.created.dlq.local` |
| Subscription do Balance | `balance-service-ledger-events-local` |
| Subscription de inspecao da DLQ de aplicacao | `ledger-events-application-dlq-inspection-local` |

O emulator e descartavel, nao usa credenciais GCP e fica fora do Terraform. Ele nao reproduz integralmente comportamento, limites e garantias do servico real. A DLQ tecnica nativa nao e simulada localmente; o topic de DLQ criado pelo init e a DLQ de aplicacao.

### Kafka local e Saga do TransferService

Kafka e o default de mensageria dos workers principais quando `Messaging:Provider` esta ausente. No compose local, ele ja faz parte do `compose.yaml`; os aliases abaixo continuam existindo apenas para compatibilidade e para deixar a intencao explicita:

```powershell
./scripts/start-local-stack-kafka.ps1
```

```bash
./scripts/start-local-stack-kafka.sh
```

O fluxo padrao tambem sobe o `TransferService.Worker`, porque a Saga orquestrada do TransferService usa Kafka como transporte explicito dos eventos da Saga e nao configura Pub/Sub. Com isso, transferencias criadas localmente podem ser processadas automaticamente pelo worker. O worker chama o `LedgerService.Api` autenticado via OAuth2 client credentials, usando `TransferService__Worker__Ledger__Auth__TokenEndpoint=http://keycloak:8080/realms/poc/protocol/openid-connect/token`, `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_CLIENT_SECRET` e scope `ledger.write`.

O compose cria os topicos Kafka abaixo de forma idempotente pelo container `kafka-init-topics`:

| Topico | Uso |
| --- | --- |
| `transfer.transferencia.solicitada` | Solicitação aceita pela API e gravada no Outbox. |
| `transfer.transferencia.debito-criado` | Debito criado no Ledger. |
| `transfer.transferencia.credito-criado` | Credito criado no Ledger. |
| `transfer.transferencia.concluida` | Saga concluida. |
| `transfer.transferencia.compensacao-solicitada` | Estorno/compensacao do debito solicitado. |
| `transfer.transferencia.compensada` | Saga compensada quando esse estado for registrado. |
| `transfer.transferencia.falhou` | Falha definitiva da Saga. |
| `transfer.transferencia.dlq` | DLQ de aplicacao para publicacao invalida ou erro definitivo da Outbox. |

Para subir apenas os componentes necessarios ao fluxo de transferencia com Kafka pelo compose, aplique migrations pelo host e use:

```bash
docker compose --env-file .env.local -f compose.yaml up -d --build postgres-db kafka kafka-init-topics keycloak ledger-service transfer-service transfer-worker
```

No host, a API de transferencias fica em `http://localhost:${TRANSFER_SERVICE_HOST_PORT:-5230}`. O Worker chama o Ledger pela rede interna em `http://ledger-service:8080`, grava evolucoes no Outbox do schema `transfer` e publica no Kafka com `transferenciaId` como message key.

Para combinar Pub/Sub emulator e observabilidade:

```powershell
./scripts/start-local-stack-pubsub.ps1 -Observability
```

```bash
OBSERVABILITY=true ./scripts/start-local-stack-pubsub.sh
```

Para inspecionar a configuracao Compose efetiva sem subir containers:

```bash
docker compose --env-file .env.local -f compose.yaml config --quiet
docker compose --env-file .env.local -f compose.yaml config
docker compose --env-file .env.local -f compose.yaml config --services
```

Se a validacao retornar erro como `required variable LEDGER_DB_PASSWORD is missing a value`, falta uma fonte de variaveis para interpolacao. Use `--env-file .env.local`, exporte as variaveis na sessao ou crie `.env` local a partir de `.env.example` quando quiser rodar `docker compose -f compose.yaml config --quiet` sem argumentos extras.

O socket Docker, mesmo montado como somente leitura, e uma superficie sensivel. Use o profile `observability` apenas em maquina local confiavel; nao use em ambiente compartilhado ou produtivo sem redesenhar a coleta de logs e revisar permissoes.

### Persistencia, tmpfs e logs Docker

O PostgreSQL local continua persistente por padrao no volume `postgres-data`. Ele nao usa `tmpfs` nem quota rigida em volume Docker nomeado, porque isso prejudicaria diagnostico local e nao e portatil entre runtimes Docker.

Dados descartaveis de observabilidade usam `tmpfs` quando seguro:

- Loki grava em `/loki` com `LOKI_TMPFS_SIZE`, default `256m`;
- Prometheus grava em `/prometheus` com `PROMETHEUS_TMPFS_SIZE`, default `512m`, retencao `PROMETHEUS_RETENTION_TIME` default `6h` e `PROMETHEUS_RETENTION_SIZE` default `512MB`;
- Alertmanager grava em `/alertmanager` com `ALERTMANAGER_TMPFS_SIZE`, default `64m`;
- Alloy grava estado local em `/var/lib/alloy/data` com `ALLOY_TMPFS_SIZE`, default `64m`;
- SonarQube mantem `sonar-postgres-data`, `sonarqube-data` e `sonarqube-extensions` persistentes, mas logs locais ficam em `tmpfs` com `SONAR_LOGS_TMPFS_SIZE`, default `256m`.

Todos os Compose principais configuram rotacao do driver Docker `json-file` com `DOCKER_LOG_MAX_SIZE` default `10m` e `DOCKER_LOG_MAX_FILE` default `3`. Isso limita crescimento dos arquivos de log do Docker sem alterar os logs emitidos pelas aplicacoes.

Para diagnosticar consumo de disco:

```powershell
./scripts/docker-disk-report.ps1
```

```bash
./scripts/docker-disk-report.sh
```

Para limpeza segura sem apagar bancos ou outros volumes:

```powershell
./scripts/docker-clean-safe.ps1
```

```bash
./scripts/docker-clean-safe.sh
```

Esses scripts usam `docker compose down --remove-orphans` sem `-v` e oferecem `docker builder prune`/`docker image prune` com confirmacao.

Reset destrutivo do PostgreSQL local:

```bash
docker compose stop ledger-service ledger-worker balance-service balance-worker postgres-db
docker compose rm -f postgres-db
docker volume rm poc-arquitetura_postgres-data
```

Esse reset apaga todos os dados locais do database `appdb`, incluindo os schemas `ledger` e `balance`, roles, grants e historico de migrations gravados no volume. Use apenas quando os dados locais forem descartaveis e suba novamente `postgres-db` ou execute `./scripts/start-local-stack.*` para recriar a topologia pelo bootstrap em `infra/postgres/init`.

### Keycloak local

O Keycloak fica no `compose.yaml` principal, e nao em overlay separado, porque ele e um componente local da propria stack de identidade. Ele faz parte da stack local padrao, pois Ledger e Balance validam tokens Keycloak por padrao no ambiente local.

Suba apenas o Keycloak:

```bash
docker compose up -d keycloak
```

Ou suba o core funcional:

```bash
docker compose up -d --build
```

Admin Console:

- `http://localhost:8081/`

Credenciais locais descartaveis:

- usuario: `local_admin`
- senha: valor local de `KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD`

Para sobrescrever porta, credenciais administrativas ou credenciais do client de automacao local, copie `.env.local.example` para `.env.local` e ajuste `KEYCLOAK_HOST_PORT`, `KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME`, `KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD`, `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_CLIENT_SECRET` e as senhas dos usuarios locais de debug. Esses valores sao apenas para desenvolvimento local e nao devem ser usados em ambientes compartilhados ou produtivos.

O container usa `start-dev --import-realm`, healthcheck nativo em `/health/ready` na porta de gerenciamento interna `9000` e importa o realm versionado de `infra/keycloak/realm-poc.json`. O compose monta esse arquivo em `/opt/keycloak/data/import/realm-poc.json` como somente leitura.

O realm local importado se chama `poc` e expoe:

- discovery OIDC: `http://localhost:8081/realms/poc/.well-known/openid-configuration`;
- JWKS: `http://localhost:8081/realms/poc/protocol/openid-connect/certs`;
- client local de automacao: `poc-automation`;
- clients locais de debug manual: `poc-local-ledger-debug`, `poc-local-balance-debug` e `poc-local-admin-debug`;
- fluxo preferencial para scripts: `client_credentials`;
- audiences: `ledger-api`, `balance-api` e `transfer-api`;
- scopes: `ledger.write`, `ledger.read`, `balance.read`, `transfer.write`, `transfer.read` e `outbox.admin`;
- claim `merchant_id`: `tese m1 m2`.

As APIs continuam usando `Jwt:JwksUrl` direto, sem introspeccao por request e sem consumir discovery metadata nesta etapa. No compose, `JWT_ISSUER` deve corresponder ao `iss` publico do token (`http://localhost:8081/realms/poc`) e `JWT_JWKS_URL` deve apontar para o endpoint de certificados acessivel pela rede interna (`http://keycloak:8080/realms/poc/protocol/openid-connect/certs`). Para voltar temporariamente ao emissor legado, suba `compose.auth-legacy.yaml` e configure `JWT_ISSUER=https://auth-api`, `JWT_JWKS_URL=http://auth-api:8080/.well-known/jwks.json` e `TOKEN_PROVIDER=auth-api`.

O segredo do client `poc-automation` vem de `KEYCLOAK_CLIENT_SECRET` no ambiente do container. O arquivo de realm usa placeholder resolvido pelo Keycloak durante `start-dev --import-realm`, mantendo o valor real fora do repositorio.

Para obter um token Keycloak local, use os scripts versionados. Eles imprimem somente o token em `stdout`:

```bash
./scripts/get-token.sh
```

No Windows:

```powershell
./scripts/get-token.ps1
```

Por padrao, `TOKEN_PROVIDER=keycloak` usa `client_credentials` com `KEYCLOAK_CLIENT_ID=poc-automation` e `KEYCLOAK_CLIENT_SECRET` definido localmente. A chamada equivalente e:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=poc-automation" \
  -d "client_secret=<KEYCLOAK_CLIENT_SECRET>"
```

Para confirmar que o realm foi importado, acesse o Admin Console em `http://localhost:8081/` com `local_admin` e o valor local de `KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD`, selecione o realm `poc` no seletor de realms e confira `Clients` e `Users`. Em uma stack recem-criada, devem existir os clients `poc-automation`, `poc-local-ledger-debug`, `poc-local-balance-debug` e `poc-local-admin-debug`, alem dos usuarios locais abaixo.

Usuarios locais de debug importados no realm:

| Usuario | Senha | Client local | Uso local | Scopes | `merchant_id` |
| --- | --- | --- | --- | --- | --- |
| `local_ledger_user` | `KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD` | `poc-local-ledger-debug` | Debug manual do LedgerService | `ledger.write ledger.read` | `tese m1` |
| `local_balance_user` | `KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD` | `poc-local-balance-debug` | Debug manual do BalanceService | `balance.read` | `tese m1` |
| `local_admin_user` | `KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD` | `poc-local-admin-debug` | Debug manual completo | `ledger.write ledger.read balance.read transfer.write transfer.read outbox.admin` | `tese m1 m2` |

Esses usuarios sao apenas conveniencia de desenvolvimento local. Eles nao devem ser usados em ambiente compartilhado, homologacao ou producao, e nao substituem `client_credentials` para automacoes.

Para obter um token manual com usuario/senha, use o client publico correspondente ao perfil:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=poc-local-ledger-debug" \
  -d "username=local_ledger_user" \
  -d "password=local_ledger_password"
```

Troque `client_id`, `username` e `password` para `poc-local-balance-debug` / `local_balance_user` ou `poc-local-admin-debug` / `local_admin_user` quando quiser validar os demais perfis. O token deve manter `iss=http://localhost:8081/realms/poc`, `aud` com as APIs autorizadas, `merchant_id` conforme o perfil e a claim `scope` conforme o perfil escolhido. Para testar `TransferService.Api`, use o token do `poc-automation` ou `poc-local-admin-debug`, que inclui `transfer-api`, `transfer.write`, `transfer.read` e merchants `m1 m2`.

Para usar temporariamente tokens emitidos pelo `Auth.Api`, suba o overlay legado e sobrescreva tambem a configuracao JWT das APIs para apontar para o emissor legado:

```bash
JWT_ISSUER=https://auth-api \
JWT_JWKS_URL=http://auth-api:8080/.well-known/jwks.json \
TOKEN_PROVIDER=auth-api ./scripts/get-token.sh
```

No Windows:

```powershell
$env:TOKEN_PROVIDER = "auth-api"
$env:JWT_ISSUER = "https://auth-api"
$env:JWT_JWKS_URL = "http://auth-api:8080/.well-known/jwks.json"
./scripts/get-token.ps1
```

Para validar discovery e JWKS:

```bash
curl -s http://localhost:8081/realms/poc/.well-known/openid-configuration
curl -s http://localhost:8081/realms/poc/protocol/openid-connect/certs
```

Nesta etapa, `Auth.Api` continua funcional apenas como legado por overlay, mas a origem padrao do JWKS usada por `LedgerService.Api` e `BalanceService.Api` no compose local e o Keycloak.

### Stack completa com observabilidade e Nginx

Use `start-full-stack.*` quando quiser um ambiente local completo para demonstracao ou validacao manual integrada:

- core funcional com APIs, workers, bancos, Kafka e init de topicos locais;
- migrations aplicadas pelo host, reaproveitando o fluxo de `start-local-stack.*`;
- profile `observability` ativo com `OTEL_ENABLED=true`;
- overlay `compose.nginx.yaml` com `nginx-edge`, `ledger-service-1` e `ledger-service-2`;
- validacoes leves de `docker compose ps`, `/health` direto e via Nginx, Grafana, Jaeger, Prometheus e Alertmanager.

Pre-requisitos adicionais:

- certificados locais em `infra/nginx/certs/localhost.crt` e `infra/nginx/certs/localhost.key`;
- portas do core funcional, observabilidade e Nginx livres no host;
- `curl` disponivel no Linux/macOS para as validacoes HTTP.

No Windows:

```powershell
./scripts/start-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-full-stack.sh
```

Para evitar rebuild de imagens:

```powershell
./scripts/start-full-stack.ps1 -NoBuild
```

```bash
./scripts/start-full-stack.sh --no-build
```

Para pular apenas as chamadas HTTP de verificacao pos-subida:

```powershell
./scripts/start-full-stack.ps1 -SkipHealthChecks
```

```bash
./scripts/start-full-stack.sh --skip-health-checks
```

Antes de subir, o script valida portas usadas pela stack completa e verifica se ha containers do overlay Nginx ou rede local do projeto em estado anterior. Quando encontra recursos locais do proprio projeto que podem prender a subida, ele pergunta se pode executar uma limpeza nao destrutiva com `docker compose down --remove-orphans`, sem `-v`. Essa limpeza para/remove containers e redes locais do projeto, mas preserva volumes, bancos locais, imagens e certificados.

Para autorizar essa limpeza previamente em fluxo nao interativo:

```powershell
./scripts/start-full-stack.ps1 -Cleanup
```

```bash
./scripts/start-full-stack.sh --cleanup
```

Se uma porta estiver ocupada por processo externo ou por container que nao pertence ao projeto, o script para e informa a porta/servico afetado. Nesse caso, libere o recurso manualmente antes de tentar novamente.

O script para com mensagem clara se os certificados do Nginx nao existirem e nao tenta gera-los automaticamente. Ele nao remove volumes, nao apaga bancos locais, nao executa testes automatizados, nao executa k6 e nao executa scanners de seguranca.

Para parar a stack completa sem remover containers, redes, volumes, bancos locais, imagens ou certificados:

```powershell
./scripts/stop-full-stack.ps1
```

```bash
./scripts/stop-full-stack.sh
```

Esse fluxo para primeiro `nginx-edge`, `ledger-service-1` e `ledger-service-2` pelos overlays locais, e depois para o core funcional com `compose.yaml` e `compose.observability.yaml`.

### Borda local HTTPS com Nginx

O Nginx local e opcional e serve como entrada HTTPS para desenvolvimento e demonstracao de load balance local do Ledger. Use-o quando quiser validar navegacao, Swagger via TLS e distribuicao de chamadas para duas instancias da `LedgerService.Api`, sem mudar contrato HTTP nem substituir o core funcional.

Antes de subir o overlay, gere ou disponibilize um certificado local em:

- `infra/nginx/certs/localhost.crt`
- `infra/nginx/certs/localhost.key`

Esses arquivos nao devem ser versionados. A opcao recomendada para certificado confiavel no host e `mkcert`:

```powershell
./scripts/generate-local-certs.ps1
```

No Linux/macOS:

```bash
./scripts/generate-local-certs.sh
```

O script usa `mkcert` quando disponivel e faz fallback para OpenSSL. Para executar manualmente com `mkcert`:

```bash
mkcert -install
mkcert -cert-file infra/nginx/certs/localhost.crt -key-file infra/nginx/certs/localhost.key localhost ledger.localhost balance.localhost
```

Alternativa com OpenSSL:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
  -keyout infra/nginx/certs/localhost.key \
  -out infra/nginx/certs/localhost.crt \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost"
```

Com OpenSSL, o navegador pode exibir alerta de certificado nao confiavel ate que o certificado seja confiado localmente.

Suba primeiro a stack local pelo fluxo normal, principalmente em banco novo para aplicar migrations. Para iniciar a borda com duas instancias do Ledger atras do Nginx, use o overlay:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Para subir tudo diretamente pelo compose sem o script de migrations:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build
```

No overlay, o servico direto `ledger-service` fica no profile `direct-ledger`. Assim, uma execucao limpa do comando acima sobe `ledger-service-1` e `ledger-service-2` para o Nginx, sem publicar porta HTTP direta do Ledger no host. O `compose.yaml` principal continua expondo `http://localhost:5226/` quando usado sem o overlay.

Se o core funcional ja estiver rodando com `ledger-service`, ele pode permanecer ativo para compatibilidade com scripts existentes; o Nginx, porem, distribui trafego somente para `ledger-service-1` e `ledger-service-2`. Para observar exatamente duas instancias do Ledger no ambiente, pare a instancia direta antes de subir o overlay:

```bash
docker compose stop ledger-service
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Portal HTTPS:

- `https://localhost:7443`

Swaggers via Nginx:

- `https://ledger.localhost:7443/swagger`
- `https://balance.localhost:7443/swagger`

Os subdominios `.localhost` evitam configurar `PathBase` nas APIs e preservam o Swagger em `/swagger`. As URLs HTTP diretas continuam disponiveis nas portas atuais e sao o alvo dos scripts e testes de carga existentes.

No overlay, o Nginx normaliza `/swagger` para a Swagger UI de cada API. Nas portas HTTP diretas atuais, a UI fica em `/index.html` e os documentos OpenAPI ficam em `/swagger/v1/swagger.json`.

TLS na borda local aceita somente `TLSv1.2` e `TLSv1.3`, desabilitando implicitamente SSLv2, SSLv3, TLSv1.0 e TLSv1.1. O overlay local nao aplica nem repassa HSTS e nao deve emitir `Strict-Transport-Security`; em `localhost`, subdominios `.localhost` e certificados autoassinados, HSTS pode ser cacheado pelo navegador e atrapalhar navegacao, rollback e alternancia entre fluxos HTTP/HTTPS de desenvolvimento. HSTS deve ser decidido apenas para ambientes apropriados fora deste fluxo local.

O Nginx adiciona uma politica basica de headers de seguranca nas respostas da borda local: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` bloqueando camera, microphone e geolocation, e `X-XSS-Protection: 0`. Para evitar duplicidade, a borda remove esses mesmos headers quando vierem das APIs internas e aplica a politica unica do proxy. O valor `0` desativa o filtro legado de XSS de navegadores antigos, evitando comportamento inconsistente; a protecao efetiva fica em controles modernos como CSP e escaping das aplicacoes. A pagina do portal tambem recebe `Content-Security-Policy` restrita ao proprio host, com `style-src 'unsafe-inline'` apenas para preservar o CSS inline estatico do portal. Os hosts dos Swaggers nao recebem CSP adicional no Nginx para evitar bloquear assets e scripts da Swagger UI.

Os hosts de API via Nginx (`ledger.localhost` e `balance.localhost`) recebem `Cache-Control: no-store` em todas as respostas proxied. Essa politica evita que respostas sensiveis de autorizacao, ledger e saldo sejam armazenadas por navegadores, clientes ou proxies intermediarios. A borda tambem remove headers de cache vindos das APIs internas e envia `Pragma: no-cache` e `Expires: 0` por compatibilidade com clientes legados. O portal estatico em `https://localhost:7443` nao recebe essa regra; os assets do Swagger servidos pelos hosts de API recebem `no-store`, o que preserva funcionamento e favorece seguranca no ambiente local.

Para reduzir fingerprinting, a borda local usa uma imagem local baseada em Alpine com Nginx e o modulo `headers-more`. A configuracao usa `server_tokens off`, remove o header `Server` emitido pela borda e remove headers de tecnologia vindos das APIs internas quando presentes, como `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` e `X-Swagger-UI-Version`.

O Nginx local tambem aplica limites defensivos basicos para reduzir abuso acidental ou malicioso antes que a chamada alcance o ASP.NET:

- `client_max_body_size 1m`, alinhado ao limite padrao `ApiLimits:MaxRequestBodySizeBytes` das APIs Ledger e Balance;
- `client_body_timeout 10s` e `client_header_timeout 10s`;
- `keepalive_timeout 30s`, `send_timeout 30s`, `proxy_connect_timeout 5s`, `proxy_send_timeout 30s` e `proxy_read_timeout 30s`;
- `large_client_header_buffers 4 8k`;
- `limit_conn` por IP em 20 conexoes simultaneas;
- `limit_req` por IP em 10 requisicoes por segundo, com `burst=40` e retorno `429 Too Many Requests`.

Payload acima de 1 MiB deve ser rejeitado na borda com `413 Payload Too Large`. Headers grandes demais podem ser rejeitados pelo Nginx antes de qualquer contrato de negocio da API. Esses limites sao uma protecao local demonstravel e nao substituem WAF, protecao DDoS, limites por usuario/merchant/client id nem dimensionamento de producao.

Validacao da politica de cache via Nginx:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://ledger.localhost:7443/health
```

As respostas dos hosts de API devem conter `Cache-Control: no-store`, `Pragma: no-cache` e `Expires: 0`. As respostas via Nginx nao devem conter `Server`, `X-Powered-By` nem `X-Swagger-UI-Version`.

O Nginx tambem atua como ponto de entrada de correlacao local:

- se o cliente enviar `X-Correlation-Id`, o valor e preservado e encaminhado para a API;
- se o cliente omitir `X-Correlation-Id`, a borda gera um identificador, encaminha para a API e devolve no response;
- o access log do Nginx e emitido como JSON por linha e inclui `correlation_id`.
- para `ledger.localhost`, o access log tambem inclui `upstream_addr` e `upstream_status`, e o response inclui `X-Upstream-Addr` para diagnostico local.

Validacao manual sem correlation id explicito:

```bash
curl -k -i https://ledger.localhost:7443/health
```

Validacao preservando um correlation id explicito:

```bash
curl -k -i -H "X-Correlation-Id: 11111111-1111-4111-8111-111111111111" https://ledger.localhost:7443/health
```

Em ambos os casos, confira o header `X-Correlation-Id` no response e o campo `correlation_id` nos logs:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Validacao de payload acima do limite:

```bash
dd if=/dev/zero of=/tmp/payload-maior-que-1m.bin bs=1024 count=1100
curl -k -i -X POST https://ledger.localhost:7443/health --data-binary @/tmp/payload-maior-que-1m.bin
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Em PowerShell:

```powershell
$payload = New-TemporaryFile
[System.IO.File]::WriteAllBytes($payload.FullName, (New-Object byte[] (1100 * 1024)))
curl.exe -k -i -X POST https://ledger.localhost:7443/health --data-binary "@$($payload.FullName)"
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
Remove-Item $payload.FullName
```

O status esperado e `413`. Como a rejeicao ocorre na borda, a rota escolhida nao precisa aceitar POST em uso normal.

Validacao de excesso de requisicoes:

```bash
for i in $(seq 1 80); do curl -k -s -o /dev/null -w "%{http_code}\n" https://ledger.localhost:7443/health & done; wait
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Em PowerShell:

```powershell
1..80 | ForEach-Object {
  Start-Job { curl.exe -k -s -o NUL -w "%{http_code}`n" https://ledger.localhost:7443/health } | Out-Null
}
Get-Job | Receive-Job -Wait -AutoRemoveJob
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Parte das chamadas pode retornar `429` quando o burst local for excedido. Chamadas normais de Swagger, portal e health devem continuar retornando os status esperados. Para diagnosticar, use os campos `status`, `request_time`, `upstream_status`, `upstream_addr` e `correlation_id` do access log JSON.

Validacao de distribuicao entre as instancias Ledger:

```bash
for i in $(seq 1 20); do curl -k -s -o /dev/null -D - https://ledger.localhost:7443/health | grep -i X-Upstream-Addr; done
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge | grep upstream_addr
docker compose -f compose.yaml -f compose.nginx.yaml logs ledger-service-1 ledger-service-2
```

Em PowerShell:

```powershell
1..20 | ForEach-Object {
  (Invoke-WebRequest -SkipCertificateCheck https://ledger.localhost:7443/health).Headers["X-Upstream-Addr"]
}
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml logs ledger-service-1 ledger-service-2
```

O Nginx open source usa uma lista estatica de upstreams nesta POC. Ele demonstra balanceamento local, mas nao implementa autoscaling real, descoberta dinamica avancada, circuit breaker ou reconfiguracao automatica quando o numero de replicas muda. Para producao, a topologia deve ser redesenhada com orquestrador, health checks e estrategia operacional propria.

As APIs executam `UseForwardedHeaders` no inicio do pipeline para reconhecer `X-Forwarded-For`, `X-Forwarded-Proto` e `X-Forwarded-Host` enviados pelo Nginx. Isso permite que componentes ASP.NET Core vejam o scheme externo `https` e o host publico `.localhost` quando a chamada entra pelo proxy, sem mudar o trafego HTTP interno entre containers.

Parar a stack:

```bash
docker compose down
```

Para a stack completa iniciada por `start-full-stack.*`, prefira:

```powershell
./scripts/stop-full-stack.ps1
```

```bash
./scripts/stop-full-stack.sh
```

Use `docker compose down` apenas quando quiser remover containers e redes do core funcional manualmente. Nao use `docker compose down -v` salvo quando a intencao for remover volumes e descartar dados locais.

Ver status e logs:

```bash
docker compose ps
docker compose logs -f ledger-service
docker compose -f compose.yaml -f compose.nginx.yaml logs -f ledger-service-1
docker compose -f compose.yaml -f compose.nginx.yaml logs -f ledger-service-2
docker compose logs -f ledger-worker
docker compose logs -f balance-service
docker compose logs -f balance-worker
docker compose -f compose.yaml -f compose.nginx.yaml logs -f nginx-edge
```

Portas expostas no host:

| Componente | URL ou porta |
| --- | --- |
| Keycloak | `http://localhost:8081/` |
| LedgerService.Api | `http://localhost:5226/` |
| BalanceService.Api | `http://localhost:5228/` |
| PostgreSQL | `localhost:15432` |
| Cloud SQL Auth Proxy | `127.0.0.1:5432` somente com `compose.cloudsql.yaml` |
| Pub/Sub emulator | `localhost:8085` com `compose.pubsub.yaml` e profile `legacy-pubsub` |
| Kafka | `localhost:19092` com `compose.yaml` |
| Jaeger UI | `http://localhost:16686/` com profile `observability` |
| Jaeger OTLP | `localhost:4317` e `localhost:4318` com profile `observability`, para diagnostico direto |
| OpenTelemetry Collector OTLP | `otel-collector:4317` e `otel-collector:4318` na rede interna do compose, com profile `observability` |
| OpenTelemetry Collector metrics | `otel-collector:9464` na rede interna do compose, com profile `observability` |
| Prometheus | `http://localhost:9090/` com profile `observability` |
| Loki | `http://localhost:3100/` com profile `observability` |
| Grafana Alloy | `http://localhost:12345/` quando o profile `observability` estiver ativo |
| Alertmanager | `http://localhost:9093/` com profile `observability` |
| Grafana | `http://localhost:3000/` com profile `observability` |
| Portal Nginx HTTPS | `https://localhost:7443/` com `compose.nginx.yaml` |
| LedgerService.Api via Nginx | `https://ledger.localhost:7443/` com `compose.nginx.yaml` |
| BalanceService.Api via Nginx | `https://balance.localhost:7443/` com `compose.nginx.yaml` |

Diferenca entre nomes internos do Compose e acesso pelo host:

| Recurso | Dentro de container do Compose | Fora do container, no host |
| --- | --- | --- |
| PostgreSQL | `postgres-db:5432` | `localhost:${POSTGRES_HOST_PORT}` ou `127.0.0.1:${POSTGRES_HOST_PORT}`; default `15432` |
| Pub/Sub emulator | `pubsub-emulator:8085` | `localhost:${PUBSUB_EMULATOR_HOST_PORT}` ou `127.0.0.1:${PUBSUB_EMULATOR_HOST_PORT}`; default `8085` |
| Kafka | `kafka:9092` | `localhost:19092` ou `127.0.0.1:19092` |

O compose sobrescreve configuracoes por variaveis de ambiente para usar hosts internos como `postgres-db`, `pubsub-emulator` e `otel-collector`. Processos executados fora do container nao resolvem esses nomes da rede Docker; nesse caso use `localhost` ou `127.0.0.1` com as portas publicadas pelo `compose.yaml`. Quando `compose.observability.yaml` e usado com `OTEL_ENABLED=true` e o profile `observability` ativo, as APIs e workers enviam OTLP somente para o Collector. O Collector encaminha traces para o Jaeger e expoe metricas em formato Prometheus para scrape interno. Prometheus coleta o Collector; Alloy coleta logs dos containers e envia para Loki. Grafana consulta Prometheus, Loki e Jaeger. O Grafana carrega automaticamente a pasta `Observability` com os dashboards `APIs - Visao Geral` e `Runtime .NET - Visao Geral`, versionados em `observability/grafana/dashboards/`. O datasource Loki possui derived field para abrir traces no datasource interno Jaeger a partir de logs com `TraceId=<valor>`. O ambiente local do compose roda como `Local`.

Prometheus tambem carrega regras locais em `observability/prometheus/rules/` e envia alertas para o Alertmanager local. A UI do Alertmanager fica em `http://localhost:9093/` e nao possui integracao externa configurada.

### Auth.Api legado

O `Auth.Api` permanece no repositorio para compatibilidade e testes do emissor legado, mas nao sobe na stack principal. Quando precisar validar esse caminho, use:

```bash
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth up -d --build auth-api
```

No modo legado, as portas e variaveis antigas continuam as mesmas: `http://localhost:5030/`, `AUTH_POC_USERNAME`, `AUTH_POC_PASSWORD`, `AUTH_POC_SCOPE` e `TOKEN_PROVIDER=auth-api`.

## Migrations via compose

O compose nao aplica migrations automaticamente. Na primeira execucao com banco vazio, e sempre que houver mudanca de schema, aplique as migrations pelo host usando as portas expostas.

Os `DbContext` usam schemas dedicados e tabelas de historico separadas:
`ledger.__EFMigrationsHistory` para o Ledger e `balance.__EFMigrationsHistory`
para o Balance. Como esta POC ja trata o PostgreSQL local como descartavel e
os schemas sao criados pelo bootstrap em `infra/postgres/init`, as migrations
atuais foram consolidadas em baselines iniciais por servico, em vez de uma
cadeia incremental para mover objetos antigos do schema `public`.

Execute as migrations dos dois servicos sequencialmente. As invocacoes de
`dotnet-ef` compilam projetos compartilhados da solution; executa-las em
paralelo pode causar disputa por artefatos em `bin/` e `obj/`.

LedgerService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=ledger_migrator_user;Password=<LEDGER_DB_MIGRATOR_PASSWORD>"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

BalanceService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=balance_migrator_user;Password=<BALANCE_DB_MIGRATOR_PASSWORD>"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

TransferService:

```powershell
$env:TRANSFER_SERVICE_CONNECTION_STRING = "Host=127.0.0.1;Port=15432;Database=appdb;Username=transfer_migrator_user;Password=<TRANSFER_DB_MIGRATOR_PASSWORD>"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\TransferService.Infrastructure\TransferService.Infrastructure.csproj `
  -s src\TransferService.Api\TransferService.Api.csproj `
  -c TransferServiceDbContext
```

## Execucao no host

Use este modo quando PostgreSQL e o provider de mensageria escolhido ja estiverem disponiveis e voce quiser rodar ou depurar os processos no host. Para execucao local fora do container, use `DOTNET_ENVIRONMENT=Local`. Os profiles de debug dos workers ja configuram `DOTNET_ENVIRONMENT=Local`. Para Pub/Sub, configure `Messaging__Provider=PubSub` e `PUBSUB_EMULATOR_HOST=127.0.0.1:8085`; para Kafka, use os bootstrap servers locais em `127.0.0.1:19092`.

Para usar configuracao por arquivo no host, copie os exemplos `appsettings.Local.example.json` para `appsettings.Local.json` quando o projeto possuir esse arquivo de exemplo:

```powershell
Copy-Item src\LedgerService.Worker\appsettings.Local.example.json src\LedgerService.Worker\appsettings.Local.json
Copy-Item src\BalanceService.Worker\appsettings.Local.example.json src\BalanceService.Worker\appsettings.Local.json
```

No Linux/macOS:

```bash
cp src/LedgerService.Worker/appsettings.Local.example.json src/LedgerService.Worker/appsettings.Local.json
cp src/BalanceService.Worker/appsettings.Local.example.json src/BalanceService.Worker/appsettings.Local.json
```

Substitua os placeholders de senha pelos valores locais e mantenha `PUBSUB_EMULATOR_HOST` como variavel de ambiente do processo, por exemplo `$env:PUBSUB_EMULATOR_HOST = "127.0.0.1:8085"` para o emulator local. Os arquivos `appsettings.Local.json` reais sao locais, ignorados pelo Git e nao devem ser versionados.

Restaure as ferramentas:

```bash
dotnet tool restore
```

Suba as APIs:

```bash
dotnet run --project src\LedgerService.Api\LedgerService.Api.csproj
dotnet run --project src\LedgerService.Worker\LedgerService.Worker.csproj
dotnet run --project src\BalanceService.Api\BalanceService.Api.csproj
dotnet run --project src\BalanceService.Worker\BalanceService.Worker.csproj
```

As portas padrao sao:

- LedgerService.Api: `http://localhost:5226/`;
- BalanceService.Api: `http://localhost:5228/`.

`LedgerService.Worker` e `BalanceService.Worker` nao expoem porta HTTP; acompanhe pelo console ou logs dos containers.

Quando APIs ou workers rodam no host contra o PostgreSQL iniciado pelo compose, use a porta publicada no host: `15432`.

## Configuracao

Configuracoes versionadas ficam nos `appsettings*.json` dos projetos de API e do Worker. Para sobrescrever valores localmente, use variaveis de ambiente com `__` como separador de secoes:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=ledger_app_user;Password=__REDACTED__"
$env:PUBSUB_EMULATOR_HOST = "127.0.0.1:8085"
$env:PubSub__Producer__ProjectId = "poc-local"
```

Nao versione segredos. Arquivos versionados de configuracao devem conter apenas placeholders ou valores nao sensiveis: nao coloque senha real, connection string com senha real, token, client secret ou credencial operacional em `appsettings*.json`, `.env.local.example` ou documentacao. Valores locais reais pertencem ao `.env.local`, a variaveis de ambiente da sessao ou aos arquivos `appsettings.Local.json` nao versionados.

Essa separacao mantem a experiencia local simples e tambem reduz ruido de ferramentas como SonarQube e Trivy: os scanners continuam analisando os arquivos versionados sem encontrar credenciais descartaveis hard-coded, enquanto cada desenvolvedor ainda consegue preencher os valores reais somente na propria maquina.

Em ambientes compartilhados ou produtivos, remova `PUBSUB_EMULATOR_HOST`, use identidade de workload quando Pub/Sub real for selecionado explicitamente e configure transporte seguro para o provider escolhido. JWKS via HTTP e Kafka `Plaintext` nao devem ser usados fora do local.

## Politica local de imagens

O compose local usa imagens com tags versionadas e nao usa `latest`. Essa escolha reduz manutencao para a POC multi-plataforma e preserva a ergonomia local.

Ambientes de CI, homologacao, producao ou qualquer ambiente compartilhado devem aplicar pinagem por digest ou scan de imagens antes da promocao. Atualizacoes de imagem precisam ser intencionais, revisaveis e registradas em diff. Se uma imagem sem digest for promovida para ambiente compartilhado, a promocao deve ser bloqueada ou justificada formalmente.

## Testcontainers e Docker-compatible API

Alguns testes de integracao usam Testcontainers com PostgreSQL real. O Testcontainers depende de uma Docker-compatible API acessivel, nao de Docker Desktop especificamente e nem da CLI usada para a stack local.

Esses testes iniciam e descartam containers PostgreSQL automaticamente durante a execucao, usando connection string dinamica e porta publicada dinamicamente pelo runtime de containers. Nao e necessario ter PostgreSQL instalado localmente e nao e necessario executar `docker compose up` antes dos testes.

Os testes PostgreSQL ficam em collections xUnit especificas, compartilham um container por collection e limpam as tabelas afetadas entre cenarios para evitar estado residual.

Validacao rapida do ambiente:

```powershell
docker version
docker ps
dotnet test
```

No Windows sem Docker Desktop, a recomendacao local e Rancher Desktop com `moby/dockerd`:

```powershell
rdctl set --container-engine.name=moby
```

Em geral, nao defina `DOCKER_HOST` de forma persistente. Com Rancher Desktop em `moby/dockerd`, a CLI `docker` e o Testcontainers devem localizar a Docker-compatible API pelo contexto/padrao do ambiente.

Nao configure `DOCKER_HOST` de forma permanente no codigo da aplicacao. Essa configuracao pertence ao ambiente local do desenvolvedor.

### Troubleshooting - Testcontainers no Windows sem Docker Desktop

Se os testes com Testcontainers falharem por nao localizar o Docker daemon:

1. Confirme que o Rancher Desktop esta usando `moby/dockerd`.
2. Confirme que a Docker API esta acessivel:

   ```powershell
   docker version
   docker ps
   ```

3. Confirme o `DOCKER_HOST`:

   ```powershell
   echo $env:DOCKER_HOST
   ```

4. Valor recomendado no Windows:

   ```text
   <vazio>
   ```

5. Se o ambiente estiver com `DOCKER_HOST=npipe:////./pipe/docker_engine`, a CLI `docker` pode funcionar, mas o Docker.DotNet usado pelo Testcontainers pode falhar com erro semelhante a `npipe:////pipe/docker_engine is not a valid npipe URI`. Remova a variavel persistente do usuario e reabra o terminal ou IDE:

   ```powershell
   [Environment]::SetEnvironmentVariable("DOCKER_HOST", $null, "User")
   ```

6. Para validar na sessao atual sem reabrir o terminal:

   ```powershell
   Remove-Item Env:DOCKER_HOST -ErrorAction SilentlyContinue
   docker ps
   dotnet test
   ```

7. Se algum runtime especifico exigir `DOCKER_HOST` para o processo de teste, use override apenas na sessao atual do terminal:

   ```powershell
   $env:DOCKER_HOST = "npipe://./pipe/docker_engine"
   dotnet test
   ```

   Evite persistir esse valor como variavel de usuario, pois ele pode quebrar a CLI `docker` em alguns ambientes Windows.

8. Feche e abra novamente o terminal ou IDE apos alterar variaveis de ambiente persistentes.

## Swagger e endpoints operacionais

Swagger/OpenAPI fica habilitado por padrao somente em `Development`. Fora desse ambiente, a exposicao exige `Swagger:Enabled=true`.

Endpoints operacionais:

- `GET /health`: liveness simples, publico nesta POC, sem depender de DB ou Kafka.
- `GET /ready`: readiness operacional, publico nesta POC. No `LedgerService.Api` e no `BalanceService.Api`, valida o banco necessario para aceitar trafego HTTP.

O compose usa healthchecks nativos para PostgreSQL e Kafka no fluxo padrao. No modo Pub/Sub legado, o emulator tambem possui healthcheck. As imagens runtime `mcr.microsoft.com/dotnet/aspnet:10.0` usadas pelas APIs nao trazem `curl`, `wget` ou `busybox`; por isso os healthchecks HTTP das APIs nao sao declarados no compose nesta etapa para evitar instalar dependencias apenas para sondas locais. Valide as APIs por `GET /health` no host ou pelos scripts/workflows que ja fazem essa chamada.

Detalhes de operacao ficam em [observabilidade e operacao minima](../observability.md).

## Limites operacionais

`LedgerService.Api` e `BalanceService.Api` possuem limites configuraveis:

- `ApiLimits:MaxRequestBodySizeBytes`;
- `ApiLimits:RateLimitPermitLimit`;
- `ApiLimits:RateLimitWindowSeconds`;
- `ApiLimits:RateLimitQueueLimit`;
- `ApiLimits:MaxBalancePeriodDays`.

Em variaveis de ambiente, use `ApiLimits__MaxRequestBodySizeBytes`, `ApiLimits__MaxBalancePeriodDays` e os demais nomes equivalentes.

## VS Code

O repositorio inclui:

- `poc-arquitetura.code-workspace`;
- `.vscode/extensions.json`;
- `.vscode/settings.json`;
- `.vscode/launch.json`;
- `.vscode/tasks.json`;
- `.vscode/rest-client.env.json`.

Para abrir:

1. Use `File > Open Workspace from File...`.
2. Selecione `poc-arquitetura.code-workspace`.
3. Instale as extensoes sugeridas.

As configuracoes do VS Code sao opcionais e apenas facilitam comandos que continuam funcionando pelo terminal. A solution padrao e `LedgerService.slnx`; as exclusoes do workspace escondem diretorios gerados como `bin`, `obj`, `TestResults`, `artifacts/k6`, relatorios de cobertura e `StrykerOutput`.

Tasks uteis:

- `dotnet: tool restore`;
- `dotnet: restore solution`;
- `dotnet: build solution`;
- `dotnet: test solution`;
- `test: coverage gate`;
- `local stack: start`;
- `local stack: start with observability`;
- `test: load smoke`.

As tasks de stack e k6 chamam os scripts versionados (`scripts/start-local-stack.*` e `scripts/run-loadtests.*`) para evitar duplicar logica. Elas nao executam teardown destrutivo nem migrations fora do fluxo ja definido pelos scripts.

As configuracoes de debug rodam processos no host em `Development` para `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`. O `Auth.Api` legado tambem possui configuracao propria, mas deve ser usado apenas quando o fluxo legado for explicitamente validado. Os nomes indicam que dependencias locais podem ser necessarias quando banco, Kafka, Pub/Sub emulator legado ou JWKS forem usados. Se a stack completa do compose estiver em execucao, pare o container equivalente antes de depurar o mesmo processo no host para evitar conflito de porta ou processamento duplicado.

O arquivo `src/LedgerService.Api/LedgerService.Api.http` pode ser usado com a extensao REST Client. Nao coloque segredos em `.vscode/rest-client.env.json`.

## Load tests com k6

Os testes de carga ficam em `loadtests/k6` e rodam dentro da rede do compose.

Pre-requisitos:

1. Suba a stack local com `./scripts/start-local-stack.ps1` ou `./scripts/start-local-stack.sh`.
2. Mantenha `LedgerService.Api`, `BalanceService.Api`, `LedgerService.Worker` e `BalanceService.Worker` em execucao quando validar cenarios que dependem de efeitos assincronos. O token padrao vem do Keycloak.

Windows:

```powershell
./scripts/run-loadtests.ps1 -Mode smoke-kafka
./scripts/run-loadtests.ps1 -Mode transfer-smoke-kafka
./scripts/run-loadtests.ps1 -Mode transfer-fullstack-kafka
./scripts/run-loadtests.ps1 -Mode load-kafka
./scripts/run-loadtests.ps1 -Mode ledger-load-kafka
./scripts/run-loadtests.ps1 -Mode transfer-load-kafka
```

Linux/macOS:

```bash
./scripts/run-loadtests.sh smoke-kafka
./scripts/run-loadtests.sh transfer-smoke-kafka
./scripts/run-loadtests.sh transfer-fullstack-kafka
./scripts/run-loadtests.sh load-kafka
./scripts/run-loadtests.sh ledger-load-kafka
./scripts/run-loadtests.sh transfer-load-kafka
```

Arquivos gerados em `artifacts/k6` e `.env.k6.auto` nao sao versionados.

Os cenarios k6 possuem thresholds locais iniciais para `checks`, `http_req_failed`, `dropped_iterations` e `http_req_duration` p95/p99. Eles sao guardrails de baseline local, nao SLO produtivo. Os valores e overrides por ambiente estao documentados em `loadtests/k6/README.md`.

Os runners aplicam `compose.k6.yaml` e recriam os containers HTTP alvo antes do k6 para manter os testes apontando para as APIs e garantir que overrides de ambiente entrem em vigor. Os workers continuam sem endpoint HTTP nos cenarios de carga. Antes de obter token e executar o k6, os runners exigem `kafka`, `kafka-init-topics`, `ledger-worker` e `balance-worker` ativos, confirmam `Messaging__Provider=Kafka`, `Kafka__Producer__BootstrapServers=kafka:9092` e `Kafka__Consumer__BootstrapServers=kafka:9092`, e validam uma conexao real no PostgreSQL usando o usuario runtime de escrita do Balance (`balance_write_user`) e `BALANCE_DB_WRITE_PASSWORD`; se a senha do volume local divergir da configuracao, o fluxo falha cedo com diagnostico e nenhuma acao destrutiva. No modo `smoke-kafka`, o runner tambem aguarda incremento de Outbox processada e de `processed_events` para confirmar publish/consume via Kafka e valida que a DLQ Kafka do Balance nao cresceu. Cloud SQL e Pub/Sub nao fazem parte do caminho k6 padrao; use os overlays e scripts explicitos quando precisar desses modos.

Os modos `transfer-smoke-kafka` e `transfer-load-kafka` usam a mesma infraestrutura k6, mas exigem apenas Keycloak, PostgreSQL e `TransferService.Api` disponiveis na stack local. Eles nao exigem conclusao full-stack da Saga em todas as iteracoes: o objetivo e validar `POST /api/v1/transferencias`, `GET /api/v1/transferencias/{transferenciaId}`, autenticacao, validacao, idempotencia e comportamento basico sob concorrencia sem aguardar a Saga finalizar.

O modo `transfer-fullstack-kafka` e o smoke manual da Saga completa. Ele usa o compose padrao com Kafka, sobe ou recria `kafka`, `kafka-init-topics`, `ledger-service`, `transfer-service` e `transfer-worker`, aguarda a transferencia chegar a `Completed` com polling controlado e valida pelos offsets e pela amostra Kafka que os eventos principais foram publicados com `message key = transferenciaId` e `correlationId` esperado. A DLQ `transfer.transferencia.dlq` nao pode crescer no fluxo feliz. Esse modo nao usa Pub/Sub para o TransferService. Se a Saga ficar `Failed` por `401 Unauthorized`, valide a configuracao `TransferService__Worker__Ledger__Auth__*`, o segredo local do Keycloak, audience `ledger-api`, scope `ledger.write` e `merchant_id` do token do worker.

O token usado nos cenarios k6 e obtido pelos runners com `scripts/get-token.*`. O fluxo oficial local e `TOKEN_PROVIDER=keycloak`, usando `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_CLIENT_SECRET`, `KEYCLOAK_REALM` e `KEYCLOAK_BASE_URL`/`KEYCLOAK_HOST_PORT`. Para usar temporariamente o fallback `Auth.Api`, suba `compose.auth-legacy.yaml` e configure tambem as APIs de negocio com `JWT_ISSUER=https://auth-api`, `JWT_JWKS_URL=http://auth-api:8080/.well-known/jwks.json` e `TOKEN_PROVIDER=auth-api`, conforme [autenticacao e autorizacao](authentication.md).

Para validar manualmente a configuracao efetiva do k6:

```bash
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config --services
```

## OWASP ZAP local

Os scripts versionados de ZAP executam DAST local em container contra `LedgerService.Api` e `BalanceService.Api` por padrao. Eles assumem que a stack ja esta rodando, validam `GET /health` antes do scan, importam `/swagger/v1/swagger.json` de cada API, salvam relatorios em `zap-reports/<timestamp>/` e removem apenas o container temporario `poc-arquitetura-zap` ao final. Para incluir o `Auth.Api` legado, suba `compose.auth-legacy.yaml` e use a opcao documentada em [OWASP ZAP local](owasp-zap.md).

URLs diretas:

```powershell
./scripts/run-owasp-zap.ps1
```

```bash
./scripts/run-owasp-zap.sh
```

Para subir a stack local direta antes do scan, use:

```powershell
./scripts/run-owasp-zap.ps1 -StartStack
```

```bash
./scripts/run-owasp-zap.sh --start-stack
```

Via Nginx local:

```powershell
./scripts/run-owasp-zap.ps1 -UseNginx
```

```bash
./scripts/run-owasp-zap.sh --use-nginx
```

Por padrao o ZAP importa os documentos OpenAPI sem injetar token. Para executar o scan com `Authorization: Bearer <token>`, use o fluxo autenticado; o token e obtido por `scripts/get-token.*`, portanto segue o mesmo padrao Keycloak e o mesmo fallback documentado para `Auth.Api`:

```powershell
./scripts/run-owasp-zap.ps1 -UseAuthentication
```

```bash
./scripts/run-owasp-zap.sh --use-authentication
```

O modo padrao usa `zap-api-scan.py -f openapi -S` e nao falha a execucao apenas por encontrar alertas. Active scan e falha por alertas exigem parametros explicitos. Detalhes ficam em [OWASP ZAP local](owasp-zap.md).

## Migrations de referencia

As migrations ficam nos projetos `Infrastructure`.

LedgerService:

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext
```

BalanceService:

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext
```

TransferService:

```bash
TRANSFER_SERVICE_CONNECTION_STRING="Host=127.0.0.1;Port=15432;Database=appdb;Username=transfer_migrator_user;Password=<TRANSFER_DB_MIGRATOR_PASSWORD>" \
dotnet tool run dotnet-ef -- migrations list \
  -p src\\TransferService.Infrastructure\\TransferService.Infrastructure.csproj \
  -s src\\TransferService.Api\\TransferService.Api.csproj \
  -c TransferServiceDbContext
```

Para criar, aplicar ou reverter migrations, use os mesmos projetos e contexts acima. Nao altere migrations antigas apenas para organizar.
