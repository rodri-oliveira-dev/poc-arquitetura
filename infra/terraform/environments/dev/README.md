# Ambiente Terraform Dev

Este root module compoe os recursos Pub/Sub e o database Cloud SQL PostgreSQL
inicial para o deployment dev. Ele habilita `pubsub.googleapis.com` e
`sqladmin.googleapis.com`, garante a identidade Pub/Sub gerenciada pelo Google
com `google_project_service_identity`, chama os modulos reutilizaveis
`pubsub-ledger-events` e `cloudsql-postgres`, e expoe outputs primitivos usados
em appsettings ou variaveis de ambiente dos adapters.

O modulo provisiona topics separados para DLQ de aplicacao e DLQ tecnica, com
subscriptions dedicadas de inspecao. Configure `PubSub:Consumer:DeadLetterTopicId`
do Balance Worker somente com o output `application_dlq_topic_name`. A DLQ
tecnica e reservada para a dead-letter policy nativa do Pub/Sub. O topic de DLQ
compartilhado existente e sua subscription de inspecao sao preservados como DLQ
de aplicacao durante a migracao de state.

O `terraform.tfvars.example` versionado habilita explicitamente a policy tecnica
nativa com `enable_technical_dead_letter=true`. Defina `false` no
`terraform.tfvars` local para rollout incremental ou testes dev que nao precisam
de encaminhamento nativo. O topic da DLQ tecnica e sua subscription de inspecao
permanecem criados, mas a policy nativa e seus bindings IAM do Pub/Sub service
agent sao omitidos. A subscription do Balance Worker e a DLQ de aplicacao
permanecem disponiveis.

O Cloud SQL dev usa por default uma instancia PostgreSQL ZONAL descartavel e de
baixo custo com edicao `ENTERPRISE`, tier `db-f1-micro`, disco de 10 GB,
`disk_autoresize=false`, `deletion_protection=false`, backups desabilitados e
point-in-time recovery desabilitado. Public IPv4 fica habilitado somente para
suportar acesso local via Cloud SQL Auth Proxy nesta primeira iteracao. O modulo
nao configura `authorized_networks`, nao permite `0.0.0.0/0`, nao cria recursos
Secret Manager e nunca expoe a senha do database como output.

Pub/Sub e Cloud SQL sao compostos intencionalmente no mesmo root module dev
nesta iteracao, portanto compartilham o mesmo backend, objeto de state, locking,
plan e ciclo de vida de apply manual. Considere esse acoplamento ao revisar
plans: um unico plan dev pode incluir drift de mensageria e database. Uma
separacao futura em root modules e prefixos de state distintos e uma divida
tecnica recomendada se o ciclo de vida do database passar a exigir ownership ou
janelas de mudanca independentes. Nao migre o state remoto automaticamente como
parte de mudancas comuns.

O exemplo versionado tambem declara as policies de expiracao de subscriptions:

| Subscription | TTL de expiracao dev | Racional |
| --- | --- | --- |
| Subscription principal do Balance Worker | `""` (nunca expira) | Preservar backlog de consumidor semelhante a producao, independente de inatividade. |
| Inspecao da DLQ de aplicacao | `"2592000s"` (30 dias) | Remover subscription de inspecao inativa em ambientes dev descartaveis. |
| Inspecao da DLQ tecnica | `"2592000s"` (30 dias) | Remover subscription de inspecao inativa em ambientes dev descartaveis. |

Todas as subscriptions mantem o default do modulo de sete dias para retencao de
mensagens nao confirmadas e usam `retain_acked_messages=false`. TTLs finitos de
expiracao devem permanecer maiores que a retencao de mensagens. Backlogs e
mensagens acumuladas em DLQ podem gerar custos de armazenamento Pub/Sub. Para
ambientes permanentes, sobrescreva o TTL de qualquer DLQ com `""` quando a
subscription de inspecao precisar sobreviver a longos periodos de inatividade.

## Residencia De Mensagens

Dev nao restringe residencia de mensagens Pub/Sub por default:

```hcl
allowed_persistence_regions = []
enforce_in_transit          = false
```

Com lista vazia, o modulo reutilizavel omite `message_storage_policy` do topic
principal, do topic da DLQ de aplicacao e do topic da DLQ tecnica. O input
`region` permanece como metadado de deployment e label; ele nao restringe onde
o Pub/Sub armazena ou processa conteudo de mensagens.

Quando um ambiente real tiver requisito de residencia aprovado, configure:

```hcl
allowed_persistence_regions = ["southamerica-east1"]
enforce_in_transit          = false
```

Revise custos de transferencia e localizacao de workloads antes de habilitar a
policy. Use `enforce_in_transit=true` com cuidado, pois o Pub/Sub pode rejeitar
requests publish, pull e streamingPull recebidos fora das regioes permitidas.

## Terraform State

Este root module configura um backend remoto parcial no Google Cloud Storage. O
objeto de state e separado por ambiente com o prefixo:

```text
poc-arquitetura/pubsub/dev
```

O nome do prefixo foi mantido para compatibilidade com o state remoto existente,
mas o objeto agora contem recursos dev de Pub/Sub e Cloud SQL. Renomear ou
separar esse prefixo exigiria migracao deliberada de state com backup, revisao
do operador e uma mudanca separada.

O nome do bucket nao fica hardcoded em `backend.tf`; forneca durante
`terraform init` com `-backend-config="bucket=rodri-terraform-state-bucket"`.
O bucket dev atual e `rodri-terraform-state-bucket`; ele foi criado fora deste
root module e nao deve ser recriado pelo mesmo state que armazena. Consulte
[`docs/adrs/0080-backend-remoto-gcs-terraform-dev.md`](../../../../docs/adrs/0080-backend-remoto-gcs-terraform-dev.md).

Conceda acesso ao bucket somente a operadores Terraform autorizados,
administradores de bootstrap/auditoria e uma identidade de CI apenas se um
workflow futuro executar `terraform plan` real. Service accounts de workloads da
aplicacao nao devem acessar o bucket de Terraform state.

Nao commite `terraform.tfvars`, arquivos de state, plans ou credenciais.

## Pre-requisitos

- Terraform CLI instalado.
- Google Cloud Application Default Credentials ou impersonation configurado
  fora do repositorio.
- Permissao para habilitar services e gerenciar recursos Pub/Sub, service
  accounts e IAM no nivel de recurso declarados pelo modulo.
- Permissao para gerar a identidade de servico do Pub/Sub pela Service Usage
  API.

## Configurar

A partir deste diretorio, crie um arquivo local de variaveis com base no exemplo
versionado e substitua o placeholder de project ID:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
```

Substitua `database_password` somente no arquivo local `terraform.tfvars`
ignorado ou forneca por `TF_VAR_database_password`. Nao commite o valor real.
Depois de um apply manual e revisado, use `database_instance_connection_name`
com Cloud SQL Auth Proxy para acesso local:

```powershell
cloud-sql-proxy "$(terraform output -raw database_instance_connection_name)" --port 5432
```

Depois aponte as connection strings locais da aplicacao para `127.0.0.1:5432`
com o nome do database e o usuario expostos pelos outputs Cloud SQL, e a senha
vinda da fonte local de segredo ignorada pelo Git.

Para smoke test local contra recursos GCP reais com ADC impersonation, defina
`service_account_token_creator_members` somente no `terraform.tfvars` local
ignorado. O modulo concede `roles/iam.serviceAccountTokenCreator` diretamente
nas duas service accounts dedicadas aos workers, nunca no nivel do projeto.
Depois do smoke test, limpe a lista e reaplique Terraform manualmente para
remover a permissao temporaria.

Para GitHub Actions, o mesmo input pode ser passado sem commitar e-mail humano:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ vars.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

ou, quando o valor estiver armazenado como secret:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ secrets.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

Para uma configuracao CI/CD mais madura, prefira Workload Identity Federation
com OIDC em vez de e-mail humano.

## Validar

Execute validacao local somente sintatica sem configurar o backend remoto:

```powershell
terraform fmt -check
terraform init -backend=false
terraform validate
terraform test
```

Esse modo de validacao e util para hooks e CI porque nao exige credenciais GCP
nem acesso ao bucket. Ele nao exercita locking de state remoto e nao deve ser
seguido por `terraform plan`, `terraform apply` ou `terraform destroy`. A suite
`terraform test` usa providers mockados e `command = plan`; ela nao cria nem
atualiza recursos GCP.

Para validacao com o backend configurado, inicialize primeiro com o bucket de
state existente:

```powershell
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform validate
```

## Migrar State Local Manualmente

Se um `terraform.tfstate` local ja existir, migre somente depois de o bucket ter
sido criado, versioning habilitado, IAM revisado e o operador ter confirmado o
projeto GCP e bucket alvo.

Crie um backup local antes da migracao:

```powershell
Copy-Item terraform.tfstate terraform.tfstate.pre-gcs-migration.backup
terraform init -migrate-state -backend-config="bucket=rodri-terraform-state-bucket"
terraform state list
terraform plan -var-file="terraform.tfvars"
```

Revise o plan com cuidado. Nao use `-lock=false`; o locking do backend GCS deve
proteger operacoes Terraform concorrentes. Nao commite backup, state,
`terraform.tfvars`, plans binarios ou credenciais.

## Plan E Apply Manuais

Revise o plan antes de qualquer mudanca remota:

```powershell
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform plan -out=tfplan
terraform apply tfplan
```

`terraform apply` e intencionalmente manual. Ele habilita as APIs Pub/Sub e
Cloud SQL Admin no projeto configurado, garante a identidade Pub/Sub gerenciada
pelo Google e provisiona recursos reais Google Cloud. Em um projeto novo,
revise se o primeiro plan inclui `google_project_service_identity.pubsub` e as
configuracoes da instancia Cloud SQL.

Nao use `-lock=false` com o backend remoto. O Terraform deve usar o locking do
backend GCS para `plan` e `apply`.

Inspecione os valores disponiveis para configuracao runtime com:

```powershell
terraform output
terraform output -json
```

Use o mapeamento de outputs para appsettings e o checklist preflight em
[`docs/development/pubsub-infra-app-contract.md`](../../../../docs/development/pubsub-infra-app-contract.md).
