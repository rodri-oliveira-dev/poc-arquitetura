# Desenvolvimento local

Este guia concentra os passos para executar, validar e depurar a POC localmente.

## Pre-requisitos

Para a stack completa:

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
- `GRAFANA_ADMIN_PASSWORD=local_dev_password`
- `AUTH_POC_USERNAME=local_user`
- `AUTH_POC_PASSWORD=local_password`
- `AUTH_POC_SCOPE=ledger.write balance.read`

Nao reutilize esses valores fora da maquina local. Em ambientes compartilhados ou produtivos, use um mecanismo proprio de secret/config store e credenciais rotacionaveis.

## Stack completa com compose

O `compose.yaml` sobe:

- `Auth.Api`;
- `LedgerService.Api`;
- `LedgerService.Worker`;
- `BalanceService.Api`;
- `BalanceService.Worker`;
- PostgreSQL Ledger;
- PostgreSQL Balance;
- Kafka single node em KRaft;
- job de inicializacao dos topicos Kafka;
- OpenTelemetry Collector como entrada local de telemetria OTLP;
- Jaeger all-in-one como backend local de visualizacao de traces;
- Prometheus para coletar metricas tecnicas expostas pelo Collector;
- Loki para armazenar logs centralizados dos containers;
- Grafana Alloy para coletar logs dos containers via Docker API, isolado no profile `observability`;
- Alertmanager local para visualizar alertas tecnicos basicos sem envio externo;
- Grafana com datasources Prometheus, Loki e Jaeger e dashboards minimos provisionados.

Subir a stack:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse fluxo sobe bancos, Kafka, observabilidade e `Auth.Api`, aplica migrations pelo host e depois inicia `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`.

Para subir somente o compose, sem aplicar migrations:

```bash
docker compose up -d --build
```

Esse comando nao inicia o Alloy por padrao, evitando montar `/var/run/docker.sock` na stack minima. Para habilitar a coleta local de logs via Docker API:

```bash
docker compose --profile observability up -d --build
```

O socket Docker, mesmo montado como somente leitura, e uma superficie sensivel. Use esse profile apenas em maquina local confiavel; nao use em ambiente compartilhado ou produtivo sem redesenhar a coleta de logs e revisar permissoes.

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
| Jaeger UI | `http://localhost:16686/` |
| Jaeger OTLP | `localhost:4317` e `localhost:4318` para diagnostico direto |
| OpenTelemetry Collector OTLP | `otel-collector:4317` e `otel-collector:4318` na rede interna do compose |
| OpenTelemetry Collector metrics | `otel-collector:9464` na rede interna do compose |
| Prometheus | `http://localhost:9090/` |
| Loki | `http://localhost:3100/` |
| Grafana Alloy | `http://localhost:12345/` quando o profile `observability` estiver ativo |
| Alertmanager | `http://localhost:9093/` |
| Grafana | `http://localhost:3000/` |

O compose sobrescreve configuracoes por variaveis de ambiente para usar hosts internos como `ledger-db`, `balance-db`, `kafka` e `otel-collector`. No compose, as APIs enviam OTLP somente para o Collector. O Collector encaminha traces para o Jaeger e expoe metricas em formato Prometheus para scrape interno. Prometheus coleta o Collector; quando o profile `observability` esta ativo, Alloy coleta logs dos containers e envia para Loki. Grafana consulta Prometheus, Loki e Jaeger. O Grafana carrega automaticamente a pasta `Observability` com os dashboards `APIs - Visao Geral` e `Runtime .NET - Visao Geral`, versionados em `observability/grafana/dashboards/`. O datasource Loki possui derived field para abrir traces no datasource interno Jaeger a partir de logs com `TraceId=<valor>`. O ambiente local do compose roda como `Development`.

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

Os runners aplicam `compose.k6.yaml` antes do k6 para manter os testes HTTP apontando para as APIs e aumentar apenas limites tecnicos de rate limiting durante a carga. Os workers continuam sem endpoint HTTP nos cenarios de carga.

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
