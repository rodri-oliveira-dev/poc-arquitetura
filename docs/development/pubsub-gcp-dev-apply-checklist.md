# Checklist manual para primeiro apply Pub/Sub em GCP dev

Este guia prepara a primeira execucao manual de
`infra/terraform/environments/dev` em um projeto GCP descartavel. O objetivo e
gerar e revisar um `terraform plan` antes de qualquer alteracao remota.

Nao automatize `terraform apply` nem `terraform destroy`. Ambos exigem decisao
humana explicita, execucao manual e conferencia do projeto selecionado.

## Escopo esperado

O root module dev deve administrar somente:

- habilitacao de `pubsub.googleapis.com`;
- identidade gerenciada do Pub/Sub;
- tres topics Pub/Sub: principal, DLQ de aplicacao e DLQ tecnica;
- tres subscriptions Pub/Sub: consumo principal e inspecao das duas DLQs;
- duas service accounts dedicadas aos workers;
- bindings IAM de menor privilegio nos topics e subscriptions.
- bindings opcionais `roles/iam.serviceAccountTokenCreator` diretamente nas
  duas service accounts dedicadas, somente para smoke test local controlado.

O Terraform nao vincula billing, nao cria projeto GCP e nao cria chaves JSON de
service account.

## Pre-requisitos

- Instalar Google Cloud CLI e Terraform CLI conforme o
  [setup local](terraform-gcp-local-setup.md).
- Autenticar o usuario que executara os comandos de leitura.
- Configurar Application Default Credentials (ADC) ou impersonation fora do
  repositorio para o provider Google usado pelo Terraform.
- Selecionar conscientemente um projeto GCP descartavel.
- Confirmar conscientemente que billing esta vinculado e habilitado nesse
  projeto. Essa acao e externa ao Terraform deste repositorio.
- Criar localmente `terraform.tfvars` a partir de
  `terraform.tfvars.example`, preencher `project_id` e revisar `region`.
- Para smoke test local com ADC impersonation, preencher
  `service_account_token_creator_members` somente no `terraform.tfvars` local.
- Nao versionar `terraform.tfvars`, state, planos binarios ou credenciais.
- Confirmar que o bucket GCS de state ja existe, possui versionamento habilitado
  e acesso restrito aos operadores Terraform autorizados. O bucket dev atual e
  `rodri-terraform-state-bucket`.
- Confirmar que o state dev usa o prefixo `poc-arquitetura/pubsub/dev`,
  conforme a [ADR-0080](../adrs/0080-backend-remoto-gcs-terraform-dev.md).

O root module habilita explicitamente apenas `pubsub.googleapis.com`. O
provider precisa acessar Service Usage para administrar essa API e usa IAM para
criar as service accounts. Em um projeto novo, confirme se
`serviceusage.googleapis.com` e o gerenciamento de service accounts via IAM
estao disponiveis. Se alguma habilitacao manual for necessaria, trate-a como
mudanca remota separada e execute-a somente apos autorizacao humana.

## Permissoes para o executor manual

Conceda permissoes temporarias e limitadas ao projeto descartavel. O executor
do primeiro apply precisa conseguir:

| Area | Necessidade |
| --- | --- |
| Service Usage | Inspecionar e habilitar `pubsub.googleapis.com`, incluindo `serviceusage.services.get`, `serviceusage.services.list` e `serviceusage.services.enable`. |
| Service identity | Gerar ou obter a identidade gerenciada do Pub/Sub usada por `google_project_service_identity.pubsub`. |
| Pub/Sub | Criar, ler, atualizar e remover topics e subscriptions; obter e alterar IAM nesses recursos. |
| Service accounts | Criar, ler e remover as duas service accounts dedicadas aos workers. |

Como ponto de partida para uma concessao temporaria em projeto descartavel,
revise os papeis predefinidos `roles/serviceusage.serviceUsageAdmin`,
`roles/pubsub.admin` e `roles/iam.serviceAccountAdmin`. Prefira uma role
customizada quando for necessario restringir melhor o conjunto de operacoes,
especialmente se a mesma identidade for reutilizada fora desta validacao.

Nao conceda `Owner`, `Editor` ou `Viewer` por conveniencia. Os bindings criados
pelo modulo para os workloads continuam restritos a `roles/pubsub.publisher` e
`roles/pubsub.subscriber` nos recursos correspondentes.

## Leitura inicial

Execute estes comandos antes de inicializar ou planejar. Confirme visualmente o
projeto e a conta ativa:

```bash
gcloud config get-value project
gcloud auth list
gcloud services list --enabled
```

`gcloud auth list` confirma a identidade do CLI, mas nao substitui a
configuracao de ADC ou impersonation usada pelo provider Terraform.

## Inicializacao e plano

Entre no root module dev e execute somente operacoes de validacao e plano:

```bash
cd infra/terraform/environments/dev
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform fmt -check
terraform validate
terraform plan -var-file="terraform.tfvars"
```

Se existir state local anterior, crie backup e migre manualmente:

```bash
cp terraform.tfstate terraform.tfstate.pre-gcs-migration.backup
terraform init -migrate-state -backend-config="bucket=rodri-terraform-state-bucket"
terraform state list
terraform plan -var-file="terraform.tfvars"
```

O comando `terraform plan` pode consultar o projeto GCP, mas nao deve criar,
alterar ou remover recursos. Revise integralmente o plano antes de autorizar
qualquer apply.

Nao use `-lock=false` com backend remoto. Essa flag contorna a protecao de
concorrencia do backend GCS e pode permitir planos ou applies simultaneos sobre
o mesmo state.

## Checklist antes do apply

- [ ] O projeto selecionado e realmente descartavel.
- [ ] O bucket de state informado no `terraform init` e `rodri-terraform-state-bucket`.
- [ ] O prefixo de state e `poc-arquitetura/pubsub/dev`.
- [ ] O billing foi habilitado conscientemente e os custos potenciais foram entendidos.
- [ ] O plano nao inclui recursos fora do escopo Pub/Sub, IAM, API e service accounts.
- [ ] Nenhuma role ampla como `Owner`, `Editor` ou `Viewer` foi concedida.
- [ ] A DLQ de aplicacao permanece separada da DLQ tecnica nativa.
- [ ] `google_project_service_identity.pubsub` fornece o service agent correto.
- [ ] Os bindings do service agent existem somente para a DLQ tecnica quando `enable_technical_dead_letter=true`.
- [ ] Os bindings opcionais `roles/iam.serviceAccountTokenCreator`, quando necessarios ao smoke test local, existem somente nas duas service accounts dedicadas.
- [ ] Os outputs permanecem compativeis com o [contrato de appsettings](pubsub-infra-app-contract.md).
- [ ] `PubSub:Consumer:DeadLetterTopicId` sera preenchido com `application_dlq_topic_name`, nunca com a DLQ tecnica.
- [ ] `allowed_persistence_regions`, `enforce_in_transit`, retencao e expiracao foram revisados para o teste.

## Apply manual

Execute `terraform apply` somente manualmente, depois da revisao integral do
plano e de autorizacao humana explicita. Antes de confirmar, confira novamente
o `project_id` de `terraform.tfvars` e o projeto retornado pelo `gcloud`.

## Checklist depois do apply

- [ ] Conferir os tres topics: principal, DLQ de aplicacao e DLQ tecnica.
- [ ] Conferir as tres subscriptions: principal e inspecao das duas DLQs.
- [ ] Conferir o IAM de topics e subscriptions, inclusive o service agent quando habilitado.
- [ ] Executar `terraform output` e `terraform output -json`.
- [ ] Configurar os appsettings ou variaveis de ambiente dos workers conforme o contrato.
- [ ] Remover `PUBSUB_EMULATOR_HOST` do ambiente usado contra GCP real.
- [ ] Executar smoke test de publish e consume, validando a projecao no Balance.
- [ ] Depois do smoke test local, limpar `service_account_token_creator_members` e reaplicar Terraform manualmente para remover a permissao temporaria.
- [ ] Se o ambiente serviu apenas para validacao descartavel, revisar e executar `terraform destroy` manualmente.
- [ ] Depois da limpeza, conferir se billing e o proprio projeto descartavel tambem devem ser desativados ou removidos manualmente.

## Riscos que exigem decisao humana

- vinculacao de billing e aceitacao de possiveis custos de throughput, backlog e DLQs;
- projeto GCP correto e descarte posterior do ambiente;
- concessao temporaria das permissoes do executor manual;
- ativacao ou nao da DLQ tecnica nativa;
- politica de residencia de mensagens e possivel custo entre regioes;
- autorizacao explicita para executar `terraform apply`;
- autorizacao explicita para executar `terraform destroy` ao encerrar a validacao.

## Referencias

- [Contrato Pub/Sub entre infraestrutura e aplicacao](pubsub-infra-app-contract.md)
- [Custo e free tier do Pub/Sub](pubsub-cost-and-free-tier.md)
- [Runbook de operacao do Pub/Sub](../operations/pubsub.md)
- [Service Usage: access control with IAM](https://cloud.google.com/service-usage/docs/access-control)
- [Pub/Sub: access control with IAM](https://cloud.google.com/pubsub/docs/access-control)
- [IAM: create service accounts](https://cloud.google.com/iam/docs/service-accounts-create)
- [Terraform Registry: google_project_service_identity](https://registry.terraform.io/providers/hashicorp/google/latest/docs/resources/project_service_identity)
