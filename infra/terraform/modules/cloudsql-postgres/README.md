# Modulo Terraform Cloud SQL PostgreSQL

Este modulo provisiona um database Google Cloud SQL for PostgreSQL para um
unico ambiente de aplicacao:

- uma instancia Cloud SQL PostgreSQL;
- um database de aplicacao;
- um usuario de database de aplicacao;
- labels padronizadas combinadas com metadados de ambiente;
- tamanho inicial de disco e comportamento de disk autoresize configuraveis;
- Public IPv4 habilitado para acesso via Cloud SQL Auth Proxy nesta primeira
  iteracao dev.

O modulo nao habilita APIs, nao cria entradas no Secret Manager, nao configura
conectividade privada VPC, nao cria bindings IAM e nao gerencia connection
strings da aplicacao. Mantenha senhas e connection strings fora de arquivos
versionados.

## Conectividade

O caminho esperado de acesso local para dev e Cloud SQL Auth Proxy usando o
output `instance_connection_name`. Public IPv4 fica habilitado, mas o modulo
nao configura `authorized_networks` e nao deve ser usado para permitir
`0.0.0.0/0`.

Exemplo de comando local do proxy depois de um apply manual e revisado a partir
do root module dev:

```powershell
cloud-sql-proxy "$(terraform output -raw database_instance_connection_name)" --port 5432
```

Configure a aplicacao localmente com host `127.0.0.1`, porta `5432`, nome do
database, usuario do database e senha vinda da fonte local de segredo ignorada
pelo Git usada para executar Terraform. A senha nunca e exposta como output do
modulo.

## Pre-requisitos

- A Cloud SQL Admin API (`sqladmin.googleapis.com`) ja deve estar habilitada no
  projeto alvo.
- A identidade que executa Terraform deve poder gerenciar instancias Cloud SQL,
  databases e usuarios.
- O caller deve fornecer `database_password` por um arquivo `terraform.tfvars`
  ignorado ou por uma variavel de ambiente segura, como
  `TF_VAR_database_password`.

## Uso

```hcl
module "cloudsql_postgres" {
  source = "../../modules/cloudsql-postgres"

  project_id        = var.project_id
  region            = var.region
  environment       = "dev"
  app_name          = "poc-ledger"
  instance_name     = "poc-ledger-dev-postgres"
  postgres_version  = "POSTGRES_16"
  tier              = "db-f1-micro"
  edition           = "ENTERPRISE"
  disk_size         = 10
  disk_autoresize   = false
  database_name     = "ledger_dev"
  database_user     = "ledger_app"
  database_password = var.database_password

  deletion_protection = false

  backup_configuration = {
    enabled                        = false
    start_time                     = "03:00"
    point_in_time_recovery_enabled = false
    transaction_log_retention_days = 7
    location                       = null
  }

  labels = {
    managed_by = "terraform"
    purpose    = "poc"
    owner      = "rodrigo"
  }
}
```

## Defaults De Backup

A configuracao default de backup mantem backups e point-in-time recovery
desabilitados para execucoes descartaveis da POC dev. Isso reduz custo
recorrente, mas todos os dados podem ser perdidos em exclusao ou falha da
instancia. Habilite backups somente depois de revisao explicita de custo e
requisitos de recuperacao.

## Saidas

O modulo expoe nome da instancia, nome de conexao da instancia, nome do
database, usuario do database, endereco IP publico quando atribuido e um objeto
de metadados nao secretos. Ele nao expoe a senha do database.

## Validacao

Execute validacao local e nao destrutiva neste diretorio:

```bash
terraform fmt -check
terraform init -backend=false
terraform validate
```

Nao execute `terraform apply` sem revisao explicita de deployment.
