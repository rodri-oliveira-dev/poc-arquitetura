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
- PostgreSQL e Kafka acessiveis localmente;
- ferramentas locais restauradas com `dotnet tool restore`.

Ferramentas opcionais:

- `curl`, para exemplos HTTP;
- VS Code, para workspace, tasks e REST Client;
- Node.js 20+, para gerar a documentacao LikeC4 localmente.

## Escopo local e credenciais

Esta stack e local, descartavel e nao deve ser promovida para ambientes compartilhados, homologacao ou producao sem revisao de seguranca, secrets, transporte, imagens e observabilidade.

O compose usa defaults ficticios para desenvolvimento local. Para sobrescrever, copie `.env.example` para `.env` e ajuste os valores localmente. O arquivo `.env` e ignorado pelo Git e nao deve ser versionado. Os defaults atuais sao intencionalmente obvios e descartaveis:

- `POSTGRES_PASSWORD=local_dev_password`
- `BALANCE_DB_HOST=balance-db`
- `BALANCE_DB_PORT=5432`
- `BALANCE_DB_HOST_PORT=15433`
- `BALANCE_DB_NAME=dbBalance`
- `BALANCE_DB_USER=userBalance`
- `BALANCE_DB_PASSWORD=local_dev_password`
- `GRAFANA_ADMIN_PASSWORD=local_dev_password`
- `AUTH_POC_USERNAME=local_user`
- `AUTH_POC_PASSWORD=local_password`
- `AUTH_POC_SCOPE=ledger.write balance.read`

As variaveis `BALANCE_DB_*` sao a origem local para o PostgreSQL do Balance no compose, para a connection string de `BalanceService.Api` e `BalanceService.Worker` dentro da rede Docker, e para os scripts que aplicam migrations ou executam load tests. Em volumes PostgreSQL existentes, alterar `.env` ou `compose.yaml` nao altera automaticamente a senha ja gravada no banco. Se houver divergencia, veja [troubleshooting](../troubleshooting.md#password-authentication-failed-for-user-userbalance).

Nao reutilize esses valores fora da maquina local. Em ambientes compartilhados ou produtivos, use um mecanismo proprio de secret/config store e credenciais rotacionaveis.

## Stack local com compose

O `compose.yaml` sobe por padrao a stack minima de desenvolvimento:

- `Auth.Api`;
- `LedgerService.Api`;
- `LedgerService.Worker`;
- `BalanceService.Api`;
- `BalanceService.Worker`;
- PostgreSQL Ledger;
- PostgreSQL Balance;
- Kafka single node em KRaft;
- job de inicializacao dos topicos Kafka.

Componentes opcionais ficam em profiles:

- profile `observability`: OpenTelemetry Collector, Jaeger, Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana;
- profile `k6`: container k6 definido em `compose.k6.yaml`.

A observabilidade completa inclui:

- OpenTelemetry Collector como entrada local de telemetria OTLP;
- Jaeger all-in-one como backend local de visualizacao de traces;
- Prometheus para coletar metricas tecnicas expostas pelo Collector;
- Loki para armazenar logs centralizados dos containers;
- Grafana Alloy para coletar logs dos containers via Docker API, isolado no profile `observability`;
- Alertmanager local para visualizar alertas tecnicos basicos sem envio externo;
- Grafana com datasources Prometheus, Loki e Jaeger e dashboards minimos provisionados.

Subir a stack minima com migrations:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse fluxo sobe bancos, Kafka e `Auth.Api`, aplica migrations pelo host e depois inicia `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`.

Para subir tambem observabilidade e habilitar exportacao OTLP nas aplicacoes:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

Para subir somente o compose, sem aplicar migrations:

```bash
docker compose up -d --build
```

Esse comando inicia apenas a stack minima. Para habilitar observabilidade completa pelo compose, incluindo coleta local de logs via Docker API, use:

```bash
OTEL_ENABLED=true docker compose --profile observability up -d --build
```

`OTEL_ENABLED=true` habilita as aplicacoes a exportarem traces e metricas para `otel-collector:4317`. Sem essa variavel, os backends de observabilidade podem subir, mas as aplicacoes permanecem com OpenTelemetry desabilitado para manter a stack minima leve.

O socket Docker, mesmo montado como somente leitura, e uma superficie sensivel. Use o profile `observability` apenas em maquina local confiavel; nao use em ambiente compartilhado ou produtivo sem redesenhar a coleta de logs e revisar permissoes.

Parar a stack:

```bash
docker compose down
```

Ver status e logs:

```bash
docker compose ps
docker compose logs -f ledger-service
docker compose logs -f ledger-worker
docker compose logs -f balance-service
docker compose logs -f balance-worker
```

Portas expostas no host:

| Componente | URL ou porta |
| --- | --- |
| Auth.Api | `http://localhost:5030/` |
| LedgerService.Api | `http://localhost:5226/` |
| BalanceService.Api | `http://localhost:5228/` |
| PostgreSQL Ledger | `localhost:15432` |
| PostgreSQL Balance | `localhost:15433` |
| Kafka | `localhost:19092` |
| Jaeger UI | `http://localhost:16686/` com profile `observability` |
| Jaeger OTLP | `localhost:4317` e `localhost:4318` com profile `observability`, para diagnostico direto |
| OpenTelemetry Collector OTLP | `otel-collector:4317` e `otel-collector:4318` na rede interna do compose, com profile `observability` |
| OpenTelemetry Collector metrics | `otel-collector:9464` na rede interna do compose, com profile `observability` |
| Prometheus | `http://localhost:9090/` com profile `observability` |
| Loki | `http://localhost:3100/` com profile `observability` |
| Grafana Alloy | `http://localhost:12345/` quando o profile `observability` estiver ativo |
| Alertmanager | `http://localhost:9093/` com profile `observability` |
| Grafana | `http://localhost:3000/` com profile `observability` |

O compose sobrescreve configuracoes por variaveis de ambiente para usar hosts internos como `ledger-db`, `balance-db`, `kafka` e `otel-collector`. Quando `OTEL_ENABLED=true` e o profile `observability` esta ativo, as APIs e workers enviam OTLP somente para o Collector. O Collector encaminha traces para o Jaeger e expoe metricas em formato Prometheus para scrape interno. Prometheus coleta o Collector; Alloy coleta logs dos containers e envia para Loki. Grafana consulta Prometheus, Loki e Jaeger. O Grafana carrega automaticamente a pasta `Observability` com os dashboards `APIs - Visao Geral` e `Runtime .NET - Visao Geral`, versionados em `observability/grafana/dashboards/`. O datasource Loki possui derived field para abrir traces no datasource interno Jaeger a partir de logs com `TraceId=<valor>`. O ambiente local do compose roda como `Development`.

Prometheus tambem carrega regras locais em `observability/prometheus/rules/` e envia alertas para o Alertmanager local. A UI do Alertmanager fica em `http://localhost:9093/` e nao possui integracao externa configurada.

## Migrations via compose

O compose nao aplica migrations automaticamente. Na primeira execucao com banco vazio, e sempre que houver mudanca de schema, aplique as migrations pelo host usando as portas expostas.

LedgerService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=local_dev_password"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

BalanceService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=local_dev_password"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

## Execucao no host

Use este modo quando PostgreSQL e Kafka ja estiverem disponiveis e voce quiser rodar ou depurar os processos no host.

Restaure as ferramentas:

```bash
dotnet tool restore
```

Suba as APIs:

```bash
dotnet run --project src\Auth.Api\Auth.Api.csproj
dotnet run --project src\LedgerService.Api\LedgerService.Api.csproj
dotnet run --project src\LedgerService.Worker\LedgerService.Worker.csproj
dotnet run --project src\BalanceService.Api\BalanceService.Api.csproj
dotnet run --project src\BalanceService.Worker\BalanceService.Worker.csproj
```

As portas padrao sao:

- Auth.Api: `http://localhost:5030/`;
- LedgerService.Api: `http://localhost:5226/`;
- BalanceService.Api: `http://localhost:5228/`.

`LedgerService.Worker` e `BalanceService.Worker` nao expoem porta HTTP; acompanhe pelo console ou logs dos containers.

## Configuracao

Configuracoes versionadas ficam nos `appsettings*.json` dos projetos de API e do Worker. Para sobrescrever valores localmente, use variaveis de ambiente com `__` como separador de secoes:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=__REDACTED__"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
```

Nao versione segredos. Em ambientes compartilhados ou produtivos, JWKS via HTTP e Kafka `Plaintext` nao devem ser usados.

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

O compose usa healthchecks nativos para PostgreSQL e Kafka. As imagens runtime `mcr.microsoft.com/dotnet/aspnet:10.0` usadas pelas APIs nao trazem `curl`, `wget` ou `busybox`; por isso os healthchecks HTTP das APIs nao sao declarados no compose nesta etapa para evitar instalar dependencias apenas para sondas locais. Valide as APIs por `GET /health` no host ou pelos scripts/workflows que ja fazem essa chamada.

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

O arquivo `src/LedgerService.Api/LedgerService.Api.http` pode ser usado com a extensao REST Client. Nao coloque segredos em `.vscode/rest-client.env.json`.

## Load tests com k6

Os testes de carga ficam em `loadtests/k6` e rodam dentro da rede do compose.

Pre-requisitos:

1. Suba a stack local com `./scripts/start-local-stack.ps1` ou `./scripts/start-local-stack.sh`.
2. Mantenha `Auth.Api`, `LedgerService.Api`, `BalanceService.Api`, `LedgerService.Worker` e `BalanceService.Worker` em execucao quando validar cenarios que dependem de efeitos assincronos.

Windows:

```powershell
./scripts/run-loadtests.ps1 -Mode smoke
./scripts/run-loadtests.ps1 -Mode balance50
./scripts/run-loadtests.ps1 -Mode resilience
```

Linux/macOS:

```bash
./scripts/run-loadtests.sh smoke
./scripts/run-loadtests.sh balance50
./scripts/run-loadtests.sh resilience
```

Arquivos gerados em `artifacts/k6` e `.env.k6.auto` nao sao versionados.

Os runners aplicam `compose.k6.yaml` e recriam os containers HTTP alvo antes do k6 para manter os testes apontando para as APIs e garantir que overrides de ambiente entrem em vigor. Os workers continuam sem endpoint HTTP nos cenarios de carga. Antes de obter token e executar o k6, os runners validam uma conexao real no PostgreSQL do Balance usando `BALANCE_DB_USER`, `BALANCE_DB_NAME` e `BALANCE_DB_PASSWORD`; se a senha do volume local divergir da configuracao, o fluxo falha cedo com diagnostico e nenhuma acao destrutiva.

Para validar manualmente a configuracao efetiva do k6:

```bash
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config --services
```

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

Para criar, aplicar ou reverter migrations, use os mesmos projetos e contexts acima. Nao altere migrations antigas apenas para organizar.
