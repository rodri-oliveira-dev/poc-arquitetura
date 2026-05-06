# Desenvolvimento local

Este guia concentra os passos para executar, validar e depurar a POC localmente.

## Pre-requisitos

Para a stack completa:

- `nerdctl` com suporte a `compose`;
- runtime compativel com containerd;
- build de imagens habilitado no runtime local.

Para rodar no host:

- .NET SDK definido em `global.json`;
- PostgreSQL e Kafka acessiveis localmente;
- ferramentas locais restauradas com `dotnet tool restore`.

Ferramentas opcionais:

- `curl`, para exemplos HTTP;
- VS Code, para workspace, tasks e REST Client;
- Node.js 20+, para gerar a documentacao LikeC4 localmente.

## Stack completa com compose

O `compose.yaml` sobe:

- `Auth.Api`;
- `LedgerService.Api`;
- `BalanceService.Api`;
- PostgreSQL Ledger;
- PostgreSQL Balance;
- Kafka single node em KRaft;
- job de inicializacao dos topicos Kafka.

Subir a stack:

```bash
nerdctl compose up -d --build
```

Parar a stack:

```bash
nerdctl compose down
```

Ver status e logs:

```bash
nerdctl compose ps
nerdctl compose logs -f ledger-service
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

O compose sobrescreve configuracoes por variaveis de ambiente para usar hosts internos como `ledger-db`, `balance-db` e `kafka`. O ambiente local do compose roda como `Development`.

## Migrations via compose

O compose nao aplica migrations automaticamente. Na primeira execucao com banco vazio, e sempre que houver mudanca de schema, aplique as migrations pelo host usando as portas expostas.

LedgerService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=app123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

BalanceService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=Balance123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

## Execucao no host

Use este modo quando PostgreSQL e Kafka ja estiverem disponiveis e voce quiser rodar ou depurar as APIs no processo local.

Restaure as ferramentas:

```bash
dotnet tool restore
```

Suba as APIs:

```bash
dotnet run --project src\Auth.Api\Auth.Api.csproj
dotnet run --project src\LedgerService.Api\LedgerService.Api.csproj
dotnet run --project src\BalanceService.Api\BalanceService.Api.csproj
```

As portas padrao sao:

- Auth.Api: `http://localhost:5030/`;
- LedgerService.Api: `http://localhost:5226/`;
- BalanceService.Api: `http://localhost:5228/`.

## Configuracao

Configuracoes versionadas ficam nos `appsettings*.json` dos projetos de API. Para sobrescrever valores localmente, use variaveis de ambiente com `__` como separador de secoes:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=__REDACTED__"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
```

Nao versione segredos. Em ambientes compartilhados ou produtivos, JWKS via HTTP e Kafka `Plaintext` nao devem ser usados.

## Swagger e endpoints operacionais

Swagger/OpenAPI fica habilitado por padrao somente em `Development`. Fora desse ambiente, a exposicao exige `Swagger:Enabled=true`.

Endpoints operacionais:

- `GET /health`: liveness simples, publico nesta POC, sem depender de DB ou Kafka.
- `GET /ready`: readiness operacional, publico nesta POC, validando DB e Kafka quando `Kafka:Enabled=true`.

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

1. Suba a stack com `nerdctl compose -f compose.yaml up -d --build`.
2. Aplique migrations.

Windows:

```powershell
./scripts/run-loadtests.ps1 -Mode smoke
./scripts/run-loadtests.ps1 -Mode balance50
./scripts/run-loadtests.ps1 -Mode resilience
```

Linux/macOS:

```bash
chmod +x ./scripts/*.sh
./scripts/run-loadtests.sh smoke
./scripts/run-loadtests.sh balance50
./scripts/run-loadtests.sh resilience
```

Arquivos gerados em `artifacts/k6` e `.env.k6.auto` nao sao versionados.

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
