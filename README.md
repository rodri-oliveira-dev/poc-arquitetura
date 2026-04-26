# poc-arquitetura

Este repositório é uma **POC (prova de conceito / laboratório de testes)** para validar decisões de arquitetura e práticas de engenharia.  
O objetivo é demonstrar, de forma reprodutível, uma base de microserviços em **.NET** usando **Clean Architecture** + **DDD**, integração assíncrona via **Kafka** (com **Outbox**) e rastreabilidade (correlação + telemetria opcional).

> Importante: por ser uma POC, alguns itens aparecem como **TODO/Proposto** (ver ADRs). Quando faltou evidência no código, foi mantido como TODO explícito.

## Serviços incluídos

- **Auth.Api** (`src/Auth.Api`)
  - Emite **JWT (RS256)** via `POST /auth/login`
  - Publica **JWKS** via `GET /.well-known/jwks.json` para validação offline pelos demais serviços
  - Aplica hardening minimo de pipeline: correlation id, security headers, ProblemDetails, HTTPS/HSTS por ambiente, Swagger gating e rate limit no login
- **LedgerService.Api** (`src/LedgerService.Api`)
  - API HTTP de **escrita** para criação de lançamentos (`POST /api/v1/lancamentos`)
  - Publica eventos via **Outbox** (entrega *at-least-once*)
- **BalanceService.Api** (`src/BalanceService.Api`)
  - API HTTP de **leitura** (consolidado diário e por período)
  - Consome eventos do Ledger e atualiza a projeção `daily_balances`

Infra local (via compose):

- **PostgreSQL** (um banco por microserviço)
- **Kafka** (single node / KRaft)

## Diagrama (Mermaid C4)

```mermaid
C4Container
title poc-arquitetura - Containers

Person(user, "Cliente", "Usuário/Sistema chamando as APIs")

System_Boundary(authBoundary, "Auth") {
  Container(authApi, "Auth.Api", "ASP.NET", "Emite JWT (RS256) e expõe JWKS")
}

System_Boundary(ledgerBoundary, "LedgerService") {
  Container(ledgerApi, "LedgerService.Api", "ASP.NET", "Cria lançamentos e grava Outbox")
  Container(outboxPublisher, "Ledger.OutboxPublisher", "Worker/HostedService", "Publica eventos do Outbox no Kafka")
  ContainerDb(ledgerDb, "PostgreSQL (ledger)", "PostgreSQL", "appdb (lancamentos + outbox)")
}

System_Boundary(balanceBoundary, "BalanceService") {
  Container(balanceApi, "BalanceService.Api", "ASP.NET", "Consulta consolidado (projeção)")
  Container(balanceConsumer, "Balance.Consumer", "Worker", "Consome eventos e atualiza projeção")
  ContainerDb(balanceDb, "PostgreSQL (balance)", "PostgreSQL", "dbBalance (daily_balances)")
}

ContainerQueue(kafka, "Kafka", "Kafka", "Tópicos de eventos (ex.: ledger.ledgerentry.created)")

Rel(user, authApi, "Login (obtém JWT)", "HTTP")
Rel(user, ledgerApi, "Cria lançamentos", "HTTP (JWT)")
Rel(user, balanceApi, "Consulta consolidado", "HTTP (JWT)")

Rel(ledgerApi, authApi, "Obtém JWKS para validar JWT", "HTTP")
Rel(balanceApi, authApi, "Obtém JWKS para validar JWT", "HTTP")
Rel(balanceConsumer, authApi, "Obtém JWKS para validar JWT (se necessário)", "HTTP")

Rel(ledgerApi, ledgerDb, "Persistência + grava Outbox", "EF Core")
Rel(outboxPublisher, ledgerDb, "Lê Outbox pendente e marca como publicado", "EF Core/SQL")
Rel(outboxPublisher, kafka, "Publica LedgerEntryCreated", "Kafka")

Rel(balanceConsumer, kafka, "Consome LedgerEntryCreated", "Kafka (consumer group)")
Rel(balanceConsumer, balanceDb, "Atualiza projeção daily_balances", "EF Core")

Rel(balanceApi, balanceDb, "Consulta projeção daily_balances", "EF Core")

```

## Arquitetura e principais componentes (por serviço)

- `src/LedgerService.Api`:
  - ASP.NET Core Controllers + Swagger
  - Middlewares: `CorrelationIdMiddleware`, `GlobalExceptionHandler`, `SecurityHeadersMiddleware`
- `src/LedgerService.Application`:
  - Casos de uso/Services (ex.: `CreateLancamentoService`)
  - FluentValidation (validators de inputs)
- `src/LedgerService.Domain`:
  - Entidades e regras de domínio (`LedgerEntry`, `OutboxMessage`, etc.)
- `src/LedgerService.Infrastructure`:
  - EF Core (`AppDbContext`) + migrations
  - Outbox publisher e integração Kafka (`OutboxKafkaPublisherService`, `OutboxKafkaProducer`)
- `tests/LedgerService.Tests`:
  - Testes unitários e de repositório

### BalanceService (consolidado)

- `src/BalanceService.Api`:
  - API HTTP (somente leitura do consolidado)
  - Swagger multi-versão
  - Middlewares: `CorrelationIdMiddleware`, `GlobalExceptionHandler`, `SecurityHeadersMiddleware`
- `src/BalanceService.Application`:
  - Handlers de queries para consulta do consolidado (diário e por período)
- `src/BalanceService.Infrastructure`:
  - EF Core (`BalanceDbContext`) + migrations
  - Consumer Kafka (`LedgerEventsConsumer`) que alimenta a tabela `daily_balances`

## Pré-requisitos

### Para executar a stack completa (recomendado)

- **nerdctl** com suporte a `compose`
  - O repo foi preparado para `nerdctl compose` (ADR-0010).
  - Você deve ter um runtime compatível com containerd (ex.: Rancher Desktop, Lima/Colima, etc. — depende do SO).
- Build de imagens/containers habilitado no seu runtime (para `--build`).

### Para executar no host (sem containers)

- **.NET SDK 10** (o repo fixa via `global.json`)
  - Versão atual: `10.0.103`.

### Para rodar testes

- `dotnet test` (já coberto pelo SDK)
- Para o script `test.sh`: **Python 3** (usado para ler o `Summary.json` do ReportGenerator)

### Ferramentas úteis (opcionais)

- `curl` (exemplos de chamadas HTTP)
- VS Code (o repo inclui workspace e recomenda extensões)

## Padronização do repositório (Git + build + estilo)

Este repositório adota alguns arquivos na raiz para garantir **consistência** entre máquinas (Windows/Linux), IDEs e CI.

### `.gitattributes`

Usamos `.gitattributes` para:

- **Normalizar EOL (line endings)** por tipo de arquivo (ex.: `*.cs` com `LF`, scripts Windows com `CRLF`).
- **Reduzir ruído em diffs e PRs** (mudanças de `CRLF/LF` deixam de poluir o histórico).
- Melhorar a experiência em **Docker/CI**, onde `LF` é o padrão.

Vantagens principais:

- Menos conflitos de merge.
- Builds/testes mais previsíveis em pipelines.
- Diferenças reais de código aparecem com clareza no git.

### `Directory.Packages.props` (Central Package Management)

Usamos `Directory.Packages.props` para habilitar **Central Package Management (CPM)** e manter as versões dos pacotes NuGet em um único lugar.

Vantagens principais:

- **Atualizações de dependências mais fáceis** (um arquivo para alterar versões).
- **Menos drift** entre projetos da solução (evita cada `.csproj` ter uma versão diferente do mesmo pacote).
- `.csproj` mais enxutos (referenciam pacotes sem `Version=`).

### `Directory.Build.props`

Usamos `Directory.Build.props` para centralizar configurações MSBuild que devem valer para todos os projetos.

Exemplos do que fica nele:

- `Nullable` e `ImplicitUsings` como defaults.
- `Deterministic`/`ContinuousIntegrationBuild` para builds mais reprodutíveis.
- Documentação XML por padrão (com supressão do warning `1591` para reduzir ruído).

Vantagens principais:

- Configuração consistente em todos os projetos.
- Evita repetição em cada `.csproj`.
- Menos chance de “um projeto estar diferente” e quebrar a solução/CI.

### `.editorconfig`

Usamos `.editorconfig` para padronizar formatação e regras de estilo entre editores/IDEs.

Vantagens principais:

- **Formatação consistente** (indentação, EOL, whitespace).
- Integração com IDEs e analisadores do .NET (regras de C# como sugestão).
- Reduz diffs apenas de formatação.

## VS Code (workspace + extensões recomendadas)

Este repositório inclui configuração para facilitar a execução no **VS Code** (com .NET 10, HTTP client e Mermaid no Markdown).

### Arquivos adicionados

- `poc-arquitetura.code-workspace`
  - define a solução padrão (`LedgerService.slnx`)
  - ajusta excludes (`bin/`, `obj/`, `.vs/`)
  - habilita Mermaid no preview de Markdown
- `.vscode/extensions.json`
  - recomenda extensões para C#/.NET, REST Client, Docker/K8s, YAML e Markdown/Mermaid
- `.vscode/settings.json`
  - configurações locais do workspace (format on save, excludes, Mermaid)
- `.vscode/launch.json` + `.vscode/tasks.json`
  - perfis de execução/debug para `LedgerService.Api` e `BalanceService.Api`
- `.vscode/rest-client.env.json`
  - variáveis por ambiente para o **REST Client** (sem segredos)

### Como abrir

No VS Code:

1. **File > Open Workspace from File...**
2. Selecione `poc-arquitetura.code-workspace`
3. Instale as extensões sugeridas quando o VS Code perguntar.

### Como chamar a API pelo VS Code

O projeto já tem um arquivo `src/LedgerService.Api/LedgerService.Api.http` (REST Client).

- Abra o arquivo `.http` e clique em **Send Request**.
- Para alternar ambientes, use a seleção de environment do REST Client.
- Variáveis ficam em `.vscode/rest-client.env.json` (ex.: `ledgerBaseUrl`, `balanceBaseUrl`).

> Importante: **não versionar segredos** nesse arquivo. Use somente URLs e valores de exemplo.

## Como executar localmente (passo a passo)

### Subir dependências e microserviços via `nerdctl compose` (stack completa)

Este repositório inclui um `compose.yaml` preparado para **nerdctl compose** (containerd), com:

- 2 bancos **PostgreSQL** (um por microserviço)
- 1 **Kafka** (single node / KRaft)
- 1 job de init para criar tópico(s) necessários
- 2 microserviços (**LedgerService.Api** e **BalanceService.Api**) na **mesma rede**

> Importante: no `BalanceService`, o consumer está com `AllowAutoCreateTopics=false`. Por isso o compose cria o tópico `ledger.ledgerentry.created` no startup.

#### Subir stack

```bash
nerdctl compose up -d --build
```

> Importante: **na primeira execução (banco vazio)** — e **sempre que houver mudança de schema** — você precisa **aplicar migrations manualmente** (ver seção **"Migrations (quando rodando via compose)"** abaixo) **antes** de usar as APIs.

#### Parar stack

```bash
nerdctl compose down
```

#### Portas expostas (host)

- LedgerService.Api: `http://localhost:5226/`
- BalanceService.Api: `http://localhost:5228/`
- Auth.Api: `http://localhost:5030/`
- PostgreSQL Ledger: `localhost:15432` (container: `ledger-db:5432`)
- PostgreSQL Balance: `localhost:15433` (container: `balance-db:5432`)
- Kafka: `localhost:19092` (container: `kafka:9092`)

#### Endpoints operacionais

- `GET /health`: liveness simples, publico, retorna `ok` e nao depende de DB/Kafka.
- `GET /ready`: readiness operacional, publico nesta PoC, valida DB e Kafka quando `Kafka:Enabled=true`.

#### Limites operacionais das APIs

`LedgerService.Api` e `BalanceService.Api` aplicam limites configuraveis para reduzir consumo excessivo de recursos:

- `ApiLimits:MaxRequestBodySizeBytes`: limite maximo do body HTTP. O padrao e `1048576` bytes. Requests acima do limite retornam `413 Payload Too Large`.
- `ApiLimits:RateLimitPermitLimit`, `ApiLimits:RateLimitWindowSeconds` e `ApiLimits:RateLimitQueueLimit`: configuram o rate limit fixo das rotas de negocio. O padrao e `100` requests por `60` segundos, com fila de `10`; rejeicoes retornam `429 Too Many Requests`.
- `ApiLimits:MaxBalancePeriodDays`: limite maximo inclusivo para `GET /v1/consolidados/periodo` no BalanceService. O padrao e `31` dias; intervalos maiores retornam `400 Bad Request`.

Em variaveis de ambiente, use o separador `__`, por exemplo `ApiLimits__MaxRequestBodySizeBytes=1048576` e `ApiLimits__MaxBalancePeriodDays=31`.

`Auth.Api` aplica o baseline minimo compativel com uma API de autenticacao: headers de seguranca, ProblemDetails para erros do pipeline, HTTPS redirection/HSTS por ambiente, Swagger gating e rate limit especifico em `POST /auth/login` via `Auth:LoginRateLimit:PermitLimit` e `Auth:LoginRateLimit:WindowSeconds`.

#### Swagger/OpenAPI por ambiente

Swagger/OpenAPI fica habilitado por padrao somente em `Development`, incluindo a execucao local via compose deste repositorio. Em qualquer outro ambiente, a exposicao exige configuracao explicita com `Swagger:Enabled=true` (ou `Swagger__Enabled=true` via variavel de ambiente).

Nao habilite Swagger em ambientes compartilhados ou produtivos sem uma excecao operacional deliberada, temporaria e protegida por controles de rede ou borda.

#### Observação sobre appsettings em container

Os `appsettings.json` usam `127.0.0.1` por padrão (para execução fora de container). No compose eu faço override por variáveis de ambiente:

- `ConnectionStrings__DefaultConnection`
- `Kafka__Producer__BootstrapServers`
- `Kafka__Consumer__BootstrapServers`
- `Kafka__Producer__SecurityProtocol=Plaintext`
- `Kafka__Consumer__SecurityProtocol=Plaintext`
- `Jwt__RequireHttpsMetadata=false`

Assim, **dentro da rede do compose** os serviços usam `ledger-db`, `balance-db` e `kafka` como hosts. O compose local executa os servicos em `Development`; por isso HTTP para JWKS e Kafka `Plaintext` sao aceitos somente nesse modo local.

#### Migrations (quando rodando via compose)

O compose **não aplica migrations automaticamente** (para evitar comportamento implícito em infraestrutura).

✅ Após subir o compose, aplique migrations a partir do host usando as portas expostas dos Postgres (**obrigatório na primeira execução/banco vazio** e sempre que houver alteração de schema):

**LedgerService (AppDbContext):**

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=app123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

**BalanceService (BalanceDbContext):**

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=Balance123"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

> TODO: se quiser automatizar migrations no startup do compose, criar um job/sidecar explícito para isso.

#### Como validar rapidamente

```bash
# Ver status dos containers
nerdctl compose ps

# Ver logs (exemplo)
nerdctl compose logs -f ledger-service
```

### Execução no host (sem containers)

Use esse modo quando você já tiver Postgres/Kafka disponíveis localmente e quiser rodar/debugar as APIs no seu processo.

#### Restaurar tools locais (dotnet-ef / reportgenerator)

O repo versiona ferramentas via `dotnet-tools.json`:

```bash
dotnet tool restore
```

#### Subir as APIs

Auth:

```bash
dotnet run --project src\Auth.Api\Auth.Api.csproj
```

Ledger:

```bash
dotnet run --project src\LedgerService.Api\LedgerService.Api.csproj
```

Balance:

```bash
dotnet run --project src\BalanceService.Api\BalanceService.Api.csproj
```

Portas (padrão do repo/launchSettings):

- Auth.Api: `http://localhost:5030/`
- LedgerService.Api: `http://localhost:5226/`
- BalanceService.Api: `http://localhost:5228/`

### Load tests (k6) via compose network

Os testes de carga ficam em `./loadtests/k6` e rodam **dentro da rede do compose** (ou seja, acessam as APIs por `http://<service_name>:<internal_port>` e **não** por `localhost`).

### Pré-requisitos

1) Suba a stack:

```bash
nerdctl compose -f compose.yaml up -d --build
```

2) Aplique migrations (ver seção **"Migrations (quando rodando via compose)"** acima).

> Nota: na prática isso é **necessário na primeira execução** (banco vazio) e sempre que o schema mudar. Se o banco já estiver migrado, este passo não altera nada.

### Execução reprodutível

Os runners fazem:

1. Gerar `.env.k6.auto` a partir do `compose.yaml` (script `scripts/compose-env.*`)
2. Obter `TOKEN` (script `scripts/get-token.*`)
3. Rodar `k6` via `nerdctl compose` (compose override `compose.k6.yaml`)
4. Exportar summary JSON em `./artifacts/k6`

#### Windows (PowerShell)

```powershell
./scripts/run-loadtests.ps1 -Mode smoke
./scripts/run-loadtests.ps1 -Mode balance50
./scripts/run-loadtests.ps1 -Mode resilience
```

#### Linux/Mac (bash)

```bash
chmod +x ./scripts/*.sh
./scripts/run-loadtests.sh smoke
./scripts/run-loadtests.sh balance50
./scripts/run-loadtests.sh resilience
```

### Overrides via variáveis de ambiente

- `AUTH_BASE_URL`, `TOKEN_URL`, `USERNAME`, `PASSWORD`, `SCOPE`
  - usados por `scripts/get-token.*`
- `TOKEN`
  - se você já tiver o JWT, pode pular o get-token e rodar k6 manualmente com `-e TOKEN=...`.
- `ALLOW_ANON=true`
  - permite rodar os scripts k6 sem token (útil para debug), mas os endpoints de negócio provavelmente vão retornar 401.

### Critérios de “passar”

- `balance_daily_50rps`:
  - `http_req_failed <= 0.05`
  - `dropped_iterations == 0`
- `ledger_resilience` (com Balance parado):
  - manter respostas `2xx/201` no Ledger

> Observação: `./artifacts/k6` e `.env.k6.auto` são gerados localmente e **não** são versionados.

### Configurar variáveis de ambiente / appsettings

Configurações ficam em:

- `src/LedgerService.Api/appsettings.json`
- `src/LedgerService.Api/appsettings.Development.json`

**Não coloque segredos no repositório.** Para execução local, use variáveis de ambiente.

Exemplos (Windows PowerShell):

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=__REDACTED__"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
```

> Observação: o formato `__` representa o separador de seções do .NET Configuration.

### Aplicar migrations

As migrations ficam nos projetos **Infrastructure** de cada serviço:

- Ledger: `LedgerService.Infrastructure` (`AppDbContext`)
- Balance: `BalanceService.Infrastructure` (`BalanceDbContext`)

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

BalanceService (`BalanceDbContext`):

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext \
  -- --environment Development
```

> O `-- --environment Development` é repassado para a aplicação (startup project) para ela carregar `appsettings.Development.json`.

### Subir o LedgerService.Api

```bash
dotnet run --project src\\LedgerService.Api\\LedgerService.Api.csproj
```

- Swagger UI (quando habilitado pela politica de ambiente): `http://localhost:5226/`
- OpenAPI JSON:
  - `http://localhost:5226/swagger/v1/swagger.json`

### Subir o BalanceService.Api

```bash
dotnet run --project src\\BalanceService.Api\\BalanceService.Api.csproj
```

- Swagger UI (quando habilitado pela politica de ambiente): `http://localhost:5228/`
- OpenAPI JSON:
  - `http://localhost:5228/swagger/v1/swagger.json`

#### Rotas de consulta (BalanceService)

- `GET /v1/consolidados/diario/{date}?merchantId={merchantId}`
- `GET /v1/consolidados/periodo?from=YYYY-MM-DD&to=YYYY-MM-DD&merchantId={merchantId}`

> Observação: padrão adotado quando não há dados é **200 com zeros** (documentado no Swagger).

## Autenticação e autorização (JWT Bearer via JWKS)

Os serviços **LedgerService.Api** e **BalanceService.Api** exigem **JWT Bearer** por padrão nas rotas de negócio.

- Validação de assinatura: **RS256** com chaves obtidas do **JWKS do Auth.Api** (`GET /.well-known/jwks.json`).
- Sem introspecção: as APIs **não chamam o Auth.Api por request**; a configuração de chaves é feita via `ConfigurationManager` (cache com refresh).
- Claim de scopes: **`scope`** (string com scopes separados por espaço). **Não** usamos `scp`.
- Claim de tenancy: **`merchant_id`** (string com um ou mais merchants separados por espaço).
- Endpoints que recebem `merchantId` no body/query exigem que o valor solicitado exista na claim `merchant_id`; caso contrario retornam **403**.
- Validação estrita:
  - `iss` deve bater com o `Jwt:Issuer` configurado.
  - `aud` deve conter a audience do serviço:
    - LedgerService.Api: `ledger-api`
    - BalanceService.Api: `balance-api`
  - Observação: nesta PoC, o Auth.Api emite `aud` como **uma string** com audiences separadas por espaço (ex.: `"ledger-api balance-api"`). As APIs tratam isso tokenizando por espaço.
- Resiliência do fetch de JWKS:
  - `Jwt:JwksTimeoutSeconds` define timeout por tentativa;
  - `Jwt:JwksRetryCount` define a quantidade de retries;
  - `Jwt:JwksRetryBaseDelayMilliseconds` define o backoff inicial.
- Transporte seguro:
  - fora de `Development`/`Local`, `Jwt:JwksUrl` deve usar `https://`;
  - `Jwt:RequireHttpsMetadata=false` e JWKS via `http://` sao aceitos apenas para execucao local;
  - o ambiente `Test` e aceito somente para `WebApplicationFactory`/testes automatizados;
  - ambientes compartilhados/produtivos devem configurar JWKS HTTPS por variaveis de ambiente ou secret/config store.

### Como obter token (Auth.Api)

1) Suba o `Auth.Api` (via compose ou `dotnet run`).
2) Configure as credenciais locais de POC por ambiente. Em `Development`, o repo traz valores de exemplo em `src/Auth.Api/appsettings.Development.json`; em outros ambientes use variaveis de ambiente/secret store, por exemplo `Auth__DevelopmentUser__Username` e `Auth__DevelopmentUser__Password`.
3) Solicite um token:

```bash
curl -s -X POST http://localhost:5030/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "poc-usuario",
    "password": "Poc#123",
    "scope": "ledger.write balance.read"
  }'
```

Nesta PoC, o usuario configurado em `Auth:DevelopmentUser` recebe a claim `merchant_id` conforme `Auth:AuthorizedMerchants`.

O campo `scope` e obrigatorio: o Auth.Api nao concede scopes implicitamente quando `scope` vem vazio/nulo. O endpoint `POST /auth/login` tambem possui rate limit especifico e pode retornar `429 Too Many Requests`.

4) Copie `access_token` do response e use nas chamadas:

```bash
curl -i http://localhost:5226/api/v1/lancamentos \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Idempotency-Key: 00000000-0000-0000-0000-000000000001" \
  -H "Content-Type: application/json" \
  -d '{"type":"CREDIT","merchantId":"tese","amount":"10.00","currency":"BRL"}'
```

> Nota: o contrato do `Auth.Api` retorna `access_token` (snake_case). Por compatibilidade, alguns scripts aceitam `accessToken` como fallback.

### Scopes por endpoint

- LedgerService.Api
  - `POST /api/v1/lancamentos`: requer `ledger.write`
- BalanceService.Api
  - `GET /v1/consolidados/diario/{date}`: requer `balance.read`
  - `GET /v1/consolidados/periodo`: requer `balance.read`

## Versionamento da API

Esta API usa **Asp.Versioning** com estratégia de **URL segment**.

- Formato: `api/v{version}/...`
- Versão padrão: `v1` (quando a versão não for especificada explicitamente)
- O Swagger UI lista automaticamente todas as versões disponíveis.

Exemplo:

- `POST /api/v1/lancamentos`

## Como rodar testes

```bash
dotnet test
```

### Rodando todos os testes com gate de coverage (>= 85% line)

Windows (PowerShell):

```powershell
./test.ps1
```

Linux/Mac:

```bash
chmod +x ./test.sh
./test.sh
```

O gate é aplicado via **coverlet** (MSBuild properties) e falha o comando caso a cobertura global da solução fique abaixo do threshold.

Exclusões aplicadas (com parcimônia, sem “forçar” coverage):

- `Program.cs` (minimal hosting)
- migrations EF (`*/Migrations/*.cs`)
- arquivos gerados (`*.g.cs`)

Os resultados ficam em `./TestResults/` (ignorado no git).

> Nota: internamente os scripts passam as exclusões via `/p:ExcludeByFile` (lista separada por vírgula).

## Banco de dados e migrations

> Nota: o exemplo abaixo usa `dotnet-ef` via `dotnet tool run` porque o repositório versiona as tools em `dotnet-tools.json`.

### LedgerService (AppDbContext)

#### Listar migrations

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext
```

#### Criar nova migration

```bash
dotnet tool run dotnet-ef -- migrations add NomeDaMigration \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -o Persistence\\Migrations
```

#### Aplicar migration

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

#### Reverter migration (quando aplicável)

O EF Core permite voltar para uma migration específica (inclusive `0`).

```bash
dotnet tool run dotnet-ef -- database update NomeDaMigrationAnteriorOu0 \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext \
  -- --environment Development
```

### BalanceService (BalanceDbContext)

#### Listar migrations

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext
```

#### Criar nova migration

```bash
dotnet tool run dotnet-ef -- migrations add NomeDaMigration \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext \
  -o Persistence\\Migrations
```

#### Aplicar migration

```bash
dotnet tool run dotnet-ef -- database update \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext \
  -- --environment Development
```

#### Reverter migration (quando aplicável)

```bash
dotnet tool run dotnet-ef -- database update NomeDaMigrationAnteriorOu0 \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext \
  -- --environment Development
```

## Kafka (se aplicável)

### Onde ficam as configurações

- Producer: `Kafka:Producer` (`src/LedgerService.Api/appsettings.json`)
- Outbox publisher: `Outbox:Publisher` (`src/LedgerService.Api/appsettings.json`)

**Exemplo (sem segredos):**

```json
{
  "Kafka": {
    "Producer": {
      "BootstrapServers": "127.0.0.1:9092",
      "ClientId": "ledger-service",
      "SecurityProtocol": "Plaintext",
      "Acks": "all",
      "EnableIdempotence": true,
      "DefaultTopic": "ledger-events",
      "TopicMap": {
        "LedgerEntryCreated.v1": "ledger.ledgerentry.created"
      }
    }
  },
  "Outbox": {
    "Publisher": {
      "PollingIntervalSeconds": 5,
      "BatchSize": 50,
      "MaxParallelism": 4,
      "MaxAttempts": 10,
      "BaseBackoffSeconds": 5,
      "LockDurationSeconds": 60
    }
  }
}
```

`SecurityProtocol=Plaintext` existe apenas para execucao local (`Development`/`Local`) e para o ambiente `Test` dos testes automatizados. Em ambientes compartilhados ou produtivos, configure `Kafka:Producer:SecurityProtocol` e `Kafka:Consumer:SecurityProtocol` como `SSL` ou `SASL_SSL` e forneca os parametros operacionais necessarios (`SslCaLocation`, `SaslMechanism`, `SaslUsername`, `SaslPassword`) via variaveis de ambiente/secret store. O projeto apenas mapeia essas opcoes para o cliente Kafka; ele nao provisiona certificados.

### Tópicos publicados

- Evento: `LedgerEntryCreated.v1`
- Tópico principal: `ledger.ledgerentry.created`
- DLQ do Balance: `ledger.ledgerentry.created.dlq`
- Mapeamento atual em `TopicMap`: `LedgerEntryCreated.v1` -> `ledger.ledgerentry.created`

### Headers publicados

Ao publicar, o producer inclui headers:

- `event_id`
- `event_type`
- `correlation_id` (quando existir)
- `traceparent` e `baggage` (quando houver `Activity`)

O `BalanceService` exige `event_type=LedgerEntryCreated.v1`, usa `event_id` para rastreabilidade/idempotência quando presente e preserva headers relevantes ao enviar mensagens para a DLQ.

### DLQ do consumer Balance

Mensagens com falha de desserialização, falha de validação de contrato/payload ou falha não recuperável de processamento são publicadas em `ledger.ledgerentry.created.dlq`.

Política de commit:

- se o processamento normal terminar com sucesso, o offset original é commitado;
- se a mensagem for publicada com sucesso na DLQ, o offset original é commitado;
- se a publicação na DLQ falhar, o offset original não é commitado.

O envelope da DLQ preserva payload original quando disponível, tópico/partição/offset originais, headers relevantes, motivo, tipo da exceção e timestamp.

### Como validar (PENDING -> SENT)

1. Aplicar migrations no PostgreSQL.
2. Subir a API.
3. Criar um lançamento via `POST /api/v1/lancamentos`.
4. Verificar no banco:
   - ao criar, surge uma linha em `outbox_messages` com `status = 'Pending'`
   - após alguns segundos, o publisher marca como `status = 'Sent'` (após confirmação do publish no Kafka)

Em caso de falha no Kafka, o serviço não cai: ele registra erro, incrementa tentativas e agenda `next_attempt_at` com backoff.

## Observabilidade e rastreabilidade

### Correlação (estado atual)

- A API usa o header `X-Correlation-Id`:
  - se ausente/inválido, gera um novo UUID;
  - retorna o mesmo header no response;
  - adiciona `CorrelationId` nos logs via logging scope.

### Traces e métricas

Consulte `docs/observability.md` para:

- arquitetura de telemetria;
- configuracao por ambiente de `Observability:OpenTelemetry`;
- uso de exporter de console e endpoint OTLP opcional;
- campos de correlação adotados (`CorrelationId`, `traceId/spanId`);
- como validar localmente.


## ADRs (decisões arquiteturais)

As decisões e pontos de melhoria do projeto estão documentados em **ADRs**:

- Pasta: [`docs/adrs/`](./docs/adrs/)
- Índice: [`docs/adrs/README.md`](./docs/adrs/README.md)

## Troubleshooting básico

- **Erro ao aplicar migrations**: confirme a connection string e se o PostgreSQL está acessível.
- **Swagger não abre**: confirme que a aplicação está rodando e acessível na URL configurada (`launchSettings.json`).
- **Outbox publisher logando queries repetidas**: comportamento esperado (polling). Ajuste `Outbox:Publisher:PollingIntervalSeconds`.
