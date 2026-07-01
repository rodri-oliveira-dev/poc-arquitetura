# Cloud SQL PostgreSQL local com Auth Proxy

Este guia prepara acesso local ao Cloud SQL PostgreSQL para debug da aplicacao
no host. O caminho esperado e:

```text
Aplicacao .NET em debug -> 127.0.0.1:5432 -> Cloud SQL Auth Proxy -> Cloud SQL PostgreSQL
```

Existem dois fluxos locais:

- Debug no host: a aplicacao .NET roda fora de container e usa
  `Host=127.0.0.1;Port=5432`.
- Docker Compose com Cloud SQL: a aplicacao roda em container e usa
  `Host=cloud-sql-proxy;Port=5432`.

No Compose local padrao, sem o overlay de Cloud SQL, o PostgreSQL continua
sendo o container `postgres-db`, publicado no host em `15432` por padrao e
acessado pelos containers como `postgres-db:5432`.

Nao configure `authorized_networks` para liberar a maquina local e nunca use
`0.0.0.0/0`. O acesso local deve passar pelo Cloud SQL Auth Proxy.

## Pre-requisitos

- Google Cloud CLI (`gcloud`) instalado.
- Cloud SQL Auth Proxy v2 instalado ou baixado localmente.
- Permissao IAM para conectar na instancia Cloud SQL, normalmente
  `roles/cloudsql.client` no projeto ou na instancia.
- Banco, usuario e senha ja criados por Terraform ou outro processo aprovado.
- Senha fora do repositorio, preferencialmente em `dotnet user-secrets`.

Instale o Google Cloud CLI pela documentacao oficial:

- Windows: <https://cloud.google.com/sdk/docs/install-sdk#windows>
- Linux/macOS: <https://cloud.google.com/sdk/docs/install>

Confirme a instalacao:

```powershell
gcloud version
gcloud auth list
```

## Autenticacao local

Autentique a conta de desenvolvimento e configure Application Default
Credentials (ADC), que sao usadas pelo Cloud SQL Auth Proxy:

```powershell
gcloud auth login
gcloud auth application-default login
gcloud config set project <PROJECT_ID>
```

Quando houver politica de impersonation, prefira ADC com a service account
aprovada:

```powershell
gcloud auth application-default login --impersonate-service-account <SERVICE_ACCOUNT_EMAIL>
```

Nao crie nem versione chave JSON de service account para este fluxo.

## Instalar o Cloud SQL Auth Proxy

Opcao simples no Windows com `gcloud`:

```powershell
gcloud components install cloud-sql-proxy
cloud-sql-proxy --version
```

Tambem e possivel baixar o binario do release oficial e deixar o executavel em
uma pasta fora do repositorio ou no `PATH` do usuario:

<https://cloud.google.com/sql/docs/postgres/connect-auth-proxy#install>

No Windows, se o binario baixado se chamar `cloud-sql-proxy.x64.exe`, renomeie
para `cloud-sql-proxy.exe` apenas no seu ambiente local.

## Obter o instance connection name

Depois de um `terraform apply` manual e revisado no root module dev:

```powershell
cd infra/terraform/environments/dev
terraform output -raw database_instance_connection_name
terraform output -raw database_name
terraform output -raw database_user
```

Sem acesso ao state Terraform, obtenha pelo `gcloud`:

```powershell
gcloud sql instances describe <INSTANCE_NAME> `
  --project <PROJECT_ID> `
  --format="value(connectionName)"
```

O formato esperado e:

```text
<PROJECT_ID>:<REGION>:<INSTANCE_NAME>
```

## Subir o proxy local

Pare qualquer PostgreSQL local usando a porta `5432` antes de iniciar o proxy.
Se estiver usando a stack Docker Compose deste repositorio, ela publica o
PostgreSQL em `15432`, entao nao deve conflitar com `5432`.

Com valor vindo do Terraform:

```powershell
cd infra/terraform/environments/dev
$env:CLOUDSQL_INSTANCE_CONNECTION_NAME = terraform output -raw database_instance_connection_name
cloud-sql-proxy $env:CLOUDSQL_INSTANCE_CONNECTION_NAME --address 127.0.0.1 --port 5432
```

Com valor informado manualmente:

```powershell
$env:CLOUDSQL_INSTANCE_CONNECTION_NAME = "<PROJECT_ID>:<REGION>:<INSTANCE_NAME>"
cloud-sql-proxy $env:CLOUDSQL_INSTANCE_CONNECTION_NAME --address 127.0.0.1 --port 5432
```

Mantenha esse terminal aberto enquanto depura a aplicacao. O proxy nao armazena
a senha do banco; ele autentica a conexao ate a instancia com IAM/ADC. A
aplicacao ainda usa usuario e senha PostgreSQL, salvo se uma decisao futura
adotar IAM database authentication.

## Subir o proxy com Docker Compose

Use `compose.cloudsql.yaml` somente para smoke manual/local contra Cloud SQL.
Ele adiciona o servico `cloud-sql-proxy`, desativa o `postgres-db` local e
sobrescreve as connection strings das APIs e workers para
`Host=cloud-sql-proxy;Port=5432`.

O proxy escuta em `0.0.0.0` dentro do container porque outros containers da
rede Compose precisam alcanca-lo. No host, a porta e publicada apenas em
`127.0.0.1:${CLOUDSQL_PROXY_HOST_PORT:-5432}`, nunca em `0.0.0.0`.

Configure um `.env` local a partir de `.env.example`:

```dotenv
CLOUDSQL_INSTANCE_CONNECTION_NAME=project-id:region:instance-name
GOOGLE_APPLICATION_CREDENTIALS=./secrets/cloudsql/application_default_credentials.json
CLOUDSQL_PROXY_HOST_PORT=5432
DATABASE_NAME=appdb
DATABASE_USER=app_user
DATABASE_PASSWORD=<DATABASE_PASSWORD>
```

`GOOGLE_APPLICATION_CREDENTIALS` deve apontar para um arquivo local ignorado
pelo Git. Pode ser o ADC gerado por `gcloud auth application-default login` ou
uma chave JSON de service account aprovada para desenvolvimento. Nao coloque o
arquivo real no repositorio e nao versione `.env`.

Exemplo com ADC copiado para uma pasta local ignorada:

```powershell
New-Item -ItemType Directory -Force .\secrets\cloudsql | Out-Null
Copy-Item "$env:APPDATA\gcloud\application_default_credentials.json" `
  .\secrets\cloudsql\application_default_credentials.json
```

Como alternativa, mantenha a credencial fora do repositorio e aponte a variavel
para o caminho absoluto local.

Para subir somente o proxy:

```bash
docker compose -f compose.yaml -f compose.cloudsql.yaml up -d cloud-sql-proxy
```

Para subir aplicacao e proxy:

```bash
docker compose -f compose.yaml -f compose.cloudsql.yaml up -d --build
```

Para validar a composicao sem iniciar containers:

```bash
docker compose -f compose.yaml -f compose.cloudsql.yaml config
docker compose -f compose.yaml -f compose.cloudsql.yaml config --services
```

Para validar a conexao pelo host, use um cliente PostgreSQL local apontando
para `127.0.0.1`:

```bash
psql "host=127.0.0.1 port=5432 dbname=<DATABASE_NAME> user=<DATABASE_USER> password=<DATABASE_PASSWORD>" -c "select 1;"
```

Para validar as APIs no Compose, use os endpoints operacionais expostos no
host:

```bash
curl http://localhost:5226/ready
curl http://localhost:5228/ready
```

O container oficial padrao do Cloud SQL Auth Proxy e distroless e nao traz
shell, `curl` ou `pg_isready`. Por isso o overlay nao declara healthcheck de
Compose para o proxy; use logs, `psql` ou `/ready` das APIs para smoke manual.

## Connection string para debug local

Use `127.0.0.1` e porta `5432` para a aplicacao em debug:

```text
Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<DATABASE_USER>;Password=<DATABASE_PASSWORD>
```

Exemplo com placeholders:

```text
Host=127.0.0.1;Port=5432;Database=appdb;Username=ledger_app_user;Password=<local-secret>
```

Nao coloque a connection string real em `appsettings.Development.json`, `.env`,
scripts, documentacao, historico do shell compartilhado ou prints. Os
`appsettings.Development.json` existentes continuam servindo como defaults
locais descartaveis; para Cloud SQL, sobrescreva por user-secrets ou variavel
de ambiente.

## Armazenar com dotnet user-secrets

Os projetos executaveis possuem `UserSecretsId` versionado para permitir
configuracao local sem gravar segredo no repositorio.

Ledger API:

```powershell
dotnet user-secrets set `
  --project src/ledger/LedgerService.Api/LedgerService.Api.csproj `
  "ConnectionStrings:DefaultConnection" `
  "Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<LEDGER_DB_USER>;Password=<DATABASE_PASSWORD>"
```

Ledger Worker:

```powershell
dotnet user-secrets set `
  --project src/ledger/LedgerService.Worker/LedgerService.Worker.csproj `
  "ConnectionStrings:DefaultConnection" `
  "Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<LEDGER_DB_USER>;Password=<DATABASE_PASSWORD>"
```

Balance API:

```powershell
dotnet user-secrets set `
  --project src/balance/BalanceService.Api/BalanceService.Api.csproj `
  "ConnectionStrings:DefaultConnection" `
  "Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<BALANCE_DB_USER>;Password=<DATABASE_PASSWORD>"
```

Balance Worker:

```powershell
dotnet user-secrets set `
  --project src/balance/BalanceService.Worker/BalanceService.Worker.csproj `
  "ConnectionStrings:DefaultConnection" `
  "Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<BALANCE_DB_USER>;Password=<DATABASE_PASSWORD>"
```

Para conferir sem exibir em logs compartilhados:

```powershell
dotnet user-secrets list --project src/ledger/LedgerService.Api/LedgerService.Api.csproj
```

Para remover um valor local:

```powershell
dotnet user-secrets remove `
  --project src/ledger/LedgerService.Api/LedgerService.Api.csproj `
  "ConnectionStrings:DefaultConnection"
```

Como alternativa temporaria, use variavel de ambiente somente no processo do
terminal:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=<DATABASE_NAME>;Username=<DATABASE_USER>;Password=<DATABASE_PASSWORD>"
dotnet run --project src/ledger/LedgerService.Api/LedgerService.Api.csproj
Remove-Item Env:ConnectionStrings__DefaultConnection
```

## Migrations e smoke local

Com o proxy rodando, aplique migrations pelo host apenas quando a instancia e o
ambiente forem os alvos corretos:

```powershell
dotnet ef database update `
  --project src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj `
  --startup-project src/ledger/LedgerService.Api/LedgerService.Api.csproj
```

Repita para Balance com o projeto de infrastructure/startup correspondente se
for necessario. Antes de rodar migrations contra Cloud SQL, confirme projeto,
instancia, database e usuario. Nao aponte testes automatizados de CI para Cloud
SQL real.

Para um smoke manual da API:

```powershell
dotnet run --project src/ledger/LedgerService.Api/LedgerService.Api.csproj
Invoke-RestMethod http://localhost:<PORTA_DA_API>/ready
```

Use a porta da API definida pelo profile local ou pelo output do `dotnet run`.

## Testes

Os testes de integracao PostgreSQL deste repositorio ja usam Testcontainers com
PostgreSQL descartavel e connection string dinamica. Eles nao dependem de Cloud
SQL, de `gcloud`, do Auth Proxy nem de senha real, e devem continuar assim para
CI.

Nao crie teste apontando diretamente para Cloud SQL real. Se precisar validar
Cloud SQL, faca smoke manual em ambiente dev, com proxy local e credenciais
aprovadas.

Os testes de integracao e os runners de carga existentes continuam usando o
PostgreSQL local/Testcontainers. Nao adapte CI para depender de Cloud SQL real.

## Troubleshooting

### Porta 5432 ocupada

Erro comum:

```text
listen tcp 127.0.0.1:5432: bind: An attempt was made to access a socket...
```

Verifique o processo usando a porta:

```powershell
Get-NetTCPConnection -LocalPort 5432 -ErrorAction SilentlyContinue
```

Pare o PostgreSQL local que usa `5432` ou rode o proxy em outra porta e ajuste a
connection string. Para este fluxo recomendado, libere `5432`.

### Credencial ADC ausente ou expirada

Sintomas: erro de login, token expirado ou mensagem pedindo credenciais.

```powershell
gcloud auth application-default login
gcloud auth application-default print-access-token
```

Se usar impersonation, confirme que sua conta pode impersonar a service account
aprovada.

### Permissao insuficiente para conectar

Sintomas: erro `403`, `not authorized` ou `Cloud SQL Admin API has not been used`.
Confirme:

```powershell
gcloud config get-value project
gcloud sql instances describe <INSTANCE_NAME> --project <PROJECT_ID>
```

A identidade usada pelo proxy precisa de permissao de Cloud SQL Client. Nao
resolva isso criando chave JSON ou abrindo authorized networks.

### Senha PostgreSQL invalida

O Auth Proxy valida IAM ate a instancia, mas a aplicacao ainda autentica no
PostgreSQL com usuario e senha. Se a API falhar em `/ready` ou o EF Core
retornar erro de autenticacao, revise o valor local em user-secrets e confirme
usuario/database pelos outputs Terraform:

```powershell
terraform output -raw database_name
terraform output -raw database_user
```

### Docker Compose conectado no banco errado

Compose padrao, Compose com Cloud SQL e debug local sao fluxos diferentes:

- Compose: containers usam `postgres-db:5432`; host usa `127.0.0.1:15432`.
- Compose com Cloud SQL: containers usam `cloud-sql-proxy:5432`; host usa
  `127.0.0.1:5432` ou `127.0.0.1:${CLOUDSQL_PROXY_HOST_PORT}`.
- Debug Cloud SQL: processo .NET no host usa `127.0.0.1:5432` com Auth Proxy.

Nao use `localhost` dentro do container da aplicacao para acessar o proxy. Em
container, `localhost` aponta para o proprio container da aplicacao, nao para o
servico `cloud-sql-proxy`. Nao use `postgres-db` para Cloud SQL e nao use
`15432` se a intencao for passar pelo Auth Proxy na porta recomendada.

## Checklist de seguranca

- Nao versionar `.env` real, `terraform.tfvars`, state, plans, chaves JSON ou
  connection strings com senha.
- Nao versionar arquivos em `secrets/`; essa pasta e apenas um ponto local
  ignorado para montar credenciais no proxy.
- Nao adicionar `authorized_networks` para acesso local.
- Nao usar `0.0.0.0/0`.
- Nao exigir Cloud SQL real em testes de CI.
- Preferir user-secrets para senha local no .NET.
- Confirmar `Host=127.0.0.1` e `Port=5432` antes de depurar contra Cloud SQL.
- Confirmar `Host=cloud-sql-proxy` e `Port=5432` quando a aplicacao rodar via
  Docker Compose com `compose.cloudsql.yaml`.
