# ADR-0080: Backend remoto GCS para Terraform dev

## Status
Aceito

## Data
2026-06-03

## Contexto
A ADR-0079 definiu os riscos do state local, os gatilhos para backend remoto e
a estrategia preferencial em Google Cloud Storage. O ambiente Terraform atual
continua concentrado em `infra/terraform/environments/dev` e administra recursos
GCP reais do fluxo Pub/Sub.

O uso de Terraform contra um ambiente dev compartilhado ou persistente exige
state centralizado, separacao por ambiente, historico operacional e locking. A
configuracao do backend deve ser adotada sem refatorar modulos, sem criar novos
ambientes e sem executar migracao automatica de state.

## Decisao
Configurar backend remoto GCS no root module
`infra/terraform/environments/dev` usando backend parcial:

- `backend "gcs"` no root module dev;
- `prefix = "poc-arquitetura/pubsub/dev"` para separar o state do ambiente dev;
- bucket dev atual `rodri-terraform-state-bucket`, fornecido manualmente em
  `terraform init` por `-backend-config`, sem versionar credenciais ou arquivos
  `.tfvars`;
- bucket de state criado fora deste root module, por bootstrap manual ou por um
  root module separado em PR proprio;
- migracao de state local para remoto somente por comando manual e explicito,
  com backup local antes de `terraform init -migrate-state`;
- nenhuma execucao automatica de `terraform apply`, `terraform destroy` ou
  import/migracao de state neste PR.

## Processo operacional
O bucket de state deve ser dedicado a Terraform state, com versionamento
habilitado e IAM minimo. Acesso ao bucket deve ser limitado a:

- operadores humanos autorizados a inicializar, planejar e aplicar Terraform;
- identidade de CI/CD somente se um fluxo futuro executar `terraform plan` real;
- administradores responsaveis por bootstrap, auditoria e recuperacao do state.

Service accounts de workloads da aplicacao nao devem acessar o bucket de state.
Quando houver multiplos ambientes, cada root module deve usar prefixo proprio,
por exemplo `poc-arquitetura/pubsub/dev`, `poc-arquitetura/pubsub/staging` ou
`poc-arquitetura/pubsub/prod`, sem compartilhar o mesmo objeto de state.

Com backend remoto habilitado, `-lock=false` nao deve ser usado em `terraform
plan` ou `terraform apply`. O locking do backend deve proteger concorrencia
entre operadores. Validacoes sintaticas sem credenciais podem continuar usando
`terraform init -backend=false`, desde que nao executem `plan`, `apply` ou
`destroy` e deixem claro que nao exercitam o locking remoto.

## Consequencias
- O state dev passa a ter configuracao remota clara e separada por ambiente.
- Inicializacao real exige informar o bucket de state existente
  `rodri-terraform-state-bucket`.
- A criacao do bucket permanece fora do root module que consome o proprio state.
- A migracao de state local para remoto continua sendo uma acao manual,
  revisavel e auditable.
- O CI atual pode continuar executando validacoes locais sem credenciais e sem
  `plan`; qualquer `plan` real futuro deve inicializar o backend remoto e nao
  desabilitar locking.

## Fora do escopo
- Criar bucket GCS.
- Migrar state automaticamente.
- Executar `terraform plan` contra GCP no CI.
- Executar `terraform apply`.
- Executar `terraform destroy`.
- Refatorar modulos Terraform ou alterar recursos Pub/Sub.
