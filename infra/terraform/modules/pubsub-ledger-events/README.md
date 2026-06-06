# Modulo Terraform Pub/Sub Ledger Events

Este modulo provisiona os recursos Google Cloud Pub/Sub necessarios para o
fluxo de eventos de `LedgerService.Worker` para `BalanceService.Worker`:

- topic principal de eventos Ledger;
- topic de DLQ de aplicacao e subscription de inspecao;
- topic de DLQ tecnica e subscription de inspecao;
- pull subscription do Balance Worker com retry policy e dead-letter policy
  nativa opcional;
- service accounts dedicadas para Ledger Worker e Balance Worker;
- bindings IAM no nivel dos recursos com as permissoes minimas exigidas pelo
  fluxo.

O modulo nao habilita APIs, nao cria secrets, nao configura credenciais e nao
cria root modules especificos de ambiente. Topics e subscriptions Pub/Sub sao
recursos globais. O input `region` e mantido como metadado de deployment para
composicao com workloads regionais. Use `allowed_persistence_regions` para
configurar uma `message_storage_policy` explicita nos tres topics quando um
ambiente tiver requisito definido de residencia de dados.

Revise o [guia de custo e free tier do Pub/Sub](../../../../docs/development/pubsub-cost-and-free-tier.md)
antes de provisionar um ambiente real. Ele registra as premissas do free tier
standard, riscos de armazenamento para backlog e DLQs, e os dados necessarios
para uma estimativa realista.

## Pre-requisitos

- A Pub/Sub API (`pubsub.googleapis.com`) ja deve estar habilitada no projeto
  alvo.
- A identidade que executa Terraform deve poder gerenciar recursos Pub/Sub,
  service accounts e os bindings IAM no nivel de recurso declarados por este
  modulo.
- O root module deve garantir o Pub/Sub service agent gerenciado pelo Google
  com `google_project_service_identity` e passar o atributo `member` para este
  modulo.

## Uso

```hcl
terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

resource "google_project_service" "pubsub" {
  project = var.project_id
  service = "pubsub.googleapis.com"

  disable_on_destroy = false
}

resource "google_project_service_identity" "pubsub" {
  provider = google-beta

  project = var.project_id
  service = google_project_service.pubsub.service
}

module "pubsub_ledger_events" {
  source = "../../modules/pubsub-ledger-events"

  project_id                            = var.project_id
  pubsub_service_agent_member           = google_project_service_identity.pubsub.member
  region                                = var.region
  allowed_persistence_regions           = ["southamerica-east1"]
  enforce_in_transit                    = false
  service_account_token_creator_members = []
  environment                           = "dev"
  app_name                              = "ledger"
  ledger_events_topic_name              = "ledger-entry-created-v1"
  ledger_events_subscription_name       = "balance-ledger-entry-created-v1"
  application_dlq_topic_name            = "ledger-entry-created-v1-dlq"
  technical_dlq_topic_name              = "ledger-entry-created-v1-technical-dlq"
  application_dlq_subscription_name     = "ledger-entry-created-v1-dlq-inspection"
  technical_dlq_subscription_name       = "ledger-entry-created-v1-technical-dlq-inspection"

  ack_deadline_seconds                       = 30
  message_retention_duration                 = "604800s"
  retain_acked_messages                      = false
  main_subscription_expiration_ttl           = ""
  application_dlq_subscription_expiration_ttl = "2592000s"
  technical_dlq_subscription_expiration_ttl   = "2592000s"
  enable_message_ordering                    = true
  enable_exactly_once_delivery               = false
  enable_technical_dead_letter               = true
  min_retry_backoff                          = "10s"
  max_retry_backoff                          = "600s"
  max_delivery_attempts                      = 5

  labels = {
    managed_by = "terraform"
  }

  depends_on = [google_project_service.pubsub]
}
```

## Modelo IAM

| Principal | Recurso | Papel | Proposito |
| --- | --- | --- | --- |
| Ledger Worker service account | Topic principal de eventos Ledger | `roles/pubsub.publisher` | Publicar eventos Ledger |
| Balance Worker service account | Pull subscription do Balance | `roles/pubsub.subscriber` | Consumir e confirmar eventos Ledger |
| Balance Worker service account | Topic da DLQ de aplicacao | `roles/pubsub.publisher` | Publicar mensagens classificadas como DLQ de aplicacao |
| Pub/Sub service agent | Topic da DLQ tecnica | `roles/pubsub.publisher` | Encaminhar mensagens para a DLQ tecnica quando `enable_technical_dead_letter=true` |
| Pub/Sub service agent | Pull subscription do Balance | `roles/pubsub.subscriber` | Confirmar mensagens encaminhadas pela dead-letter policy nativa quando `enable_technical_dead_letter=true` |
| Membros configurados para smoke test local | Service accounts do Ledger Worker e Balance Worker | `roles/iam.serviceAccountTokenCreator` | Impersonar identidades dedicadas dos workers somente quando configurado explicitamente em ambiente controlado |

A DLQ de aplicacao e publicada diretamente pelo Balance Worker. A DLQ tecnica e
usada apenas pela dead-letter policy nativa na subscription principal.
Subscriptions de inspecao separadas retêm cada fluxo de forma independente para
triagem, alertas e decisoes de reprocessamento.

`service_account_token_creator_members` usa lista vazia por default. Use apenas
para smoke tests locais controlados que precisam de ADC impersonation, mantendo
valores reais de membros fora de arquivos versionados.

## Retencao E Expiracao

Todas as subscriptions retêm mensagens nao confirmadas por sete dias por
default (`message_retention_duration = "604800s"`) e nao retêm mensagens
confirmadas (`retain_acked_messages = false`). Backlogs nao processados e
mensagens acumuladas em DLQ nessa janela podem gerar custos de armazenamento
Pub/Sub.

Cada subscription declara uma `expiration_policy` explicita. Defina o input TTL
como string vazia (`""`) para manter a subscription independentemente da
inatividade. Use uma duracao Google como `"2592000s"` para remover uma
subscription inativa depois de um periodo finito. Qualquer TTL finito de
expiracao deve ser maior que `message_retention_duration`.

Os defaults do modulo reutilizavel mantem a subscription principal do Balance
Worker persistente e expiram subscriptions de inspecao das DLQs de aplicacao e
tecnica depois de 30 dias de inatividade. Ambientes permanentes podem
sobrescrever o TTL de qualquer DLQ com `""` quando preservar subscriptions de
inspecao por longos periodos inativos for operacionalmente necessario.

Defina `enable_technical_dead_letter=false` para omitir a dead-letter policy
nativa da subscription principal durante rollout incremental ou testes dev com
controle de custo. A subscription principal, DLQ de aplicacao, topic da DLQ
tecnica e subscription de inspecao da DLQ tecnica continuam provisionados.
Somente os bindings IAM do service agent usados pelo fluxo tecnico nativo sao
omitidos.

Os blocos `moved` preservam o antigo topic e a subscription compartilhados de
DLQ como DLQ de aplicacao durante a migracao de state. Os recursos da DLQ
tecnica sao novos.

## Policy De Armazenamento De Mensagens

O modulo aplica a mesma `message_storage_policy` opcional ao topic principal de
eventos Ledger, ao topic da DLQ de aplicacao e ao topic da DLQ tecnica. Os
defaults sao:

```hcl
allowed_persistence_regions = []
enforce_in_transit          = false
```

Uma lista vazia em `allowed_persistence_regions` omite o bloco de policy porque
o provider nao aceita uma policy vazia configurada explicitamente. Nesse modo,
o input `region` permanece como label e nao restringe onde o Pub/Sub armazena
ou processa conteudo das mensagens.

Para um ambiente com decisao aprovada de residencia, configure:

```hcl
allowed_persistence_regions = ["southamerica-east1"]
enforce_in_transit          = false
```

Uma storage policy pode adicionar custos de transferencia de dados entre
regioes quando mensagens precisam sair da regiao do publisher ou subscriber.
Defina `enforce_in_transit=true` somente depois de revisar localizacoes de
clients e endpoints: o Pub/Sub pode rejeitar requests publish, pull e
streamingPull recebidos fora das regioes permitidas.

## Validacao

Execute validacao local e nao destrutiva neste diretorio:

```bash
terraform fmt -check
terraform init -backend=false
terraform validate
```

Nao execute `terraform apply` sem revisao explicita de deployment.
