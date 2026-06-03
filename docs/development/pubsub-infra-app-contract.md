# Contrato Pub/Sub entre infraestrutura e aplicacao

Este documento registra como os recursos reais criados por
`infra/terraform/environments/dev` preenchem as options dos workers. Os clients
.NET recebem nomes simples e compoem os resource names com `ProjectId`; nao use
os IDs qualificados `projects/{project}/topics/{topic}` nas options.

Os perfis `appsettings.PubSub.json` versionados pertencem apenas ao emulator
local e usam nomes `*.local`. Contra GCP real, use os outputs Terraform `*.dev`.

## Outputs Terraform para appsettings

| Terraform output | Appsettings key | Observacao |
| --- | --- | --- |
| Nao se aplica | `Messaging:Provider` | Configuracao runtime dos dois workers. Defina `PubSub` para selecionar o adapter. |
| Nao se aplica | `PubSub:Enabled` | Feature switch runtime dos dois workers, com default `true`. Nao representa recurso provisionado. |
| `project_id` | `PubSub:Producer:ProjectId` | Projeto GCP do `LedgerService.Worker`. |
| `ledger_events_topic_name` | `PubSub:Producer:DefaultTopicId` | Topic ID simples: `ledger.ledgerentry.created.dev`. |
| `ledger_events_topic_map` | `PubSub:Producer:TopicMap` | Mapeia `LedgerEntryCreated.v1` para o topic principal. |
| `enable_message_ordering` | `PubSub:Producer:EnableMessageOrdering` | Deve permanecer alinhado com a subscription. |
| `project_id` | `PubSub:Consumer:ProjectId` | Projeto GCP do `BalanceService.Worker`. |
| `ledger_events_subscription_name` | `PubSub:Consumer:SubscriptionId` | Subscription ID simples consumida pelo Balance. |
| `application_dlq_topic_name` | `PubSub:Consumer:DeadLetterTopicId` | Topic ID simples usado exclusivamente pela DLQ de aplicacao. |
| `enable_exactly_once_delivery` | `PubSub:Consumer:EnableExactlyOnceDelivery` | Espelha a configuracao da subscription. |
| `ack_deadline_seconds` | `PubSub:Consumer:AckDeadlineSeconds` | Espelha a configuracao da subscription. |
| Nao se aplica | `PubSub:Consumer:ProcessingErrorRetryDelay` | Option local do worker, com default `00:00:05`. A retry policy nativa governa o redelivery. |

O adapter responde `nack` em falhas recuperaveis e a retry policy nativa da
subscription governa o redelivery.

## IAM minimo

| Service account | Permissao | Motivo |
| --- | --- | --- |
| Ledger Worker | `roles/pubsub.publisher` no topic principal | Publicar eventos da Outbox. |
| Balance Worker | `roles/pubsub.subscriber` na subscription principal | Consumir e confirmar eventos financeiros. |
| Balance Worker | `roles/pubsub.publisher` no topic da DLQ de aplicacao | Publicar mensagens classificadas pela aplicacao. |
| Pub/Sub service agent | `roles/pubsub.publisher` no topic da DLQ tecnica | Encaminhar mensagens pela dead-letter policy nativa quando `enable_technical_dead_letter=true`. |
| Pub/Sub service agent | `roles/pubsub.subscriber` na subscription principal | Confirmar mensagens encaminhadas pela dead-letter policy nativa quando `enable_technical_dead_letter=true`. |
| Membros configurados para smoke test local | `roles/iam.serviceAccountTokenCreator` diretamente nas duas service accounts dos workers | Permitir ADC impersonation temporaria somente em ambiente dev/controlado. |

O modulo nao concede `Owner`, `Editor` nem `Viewer` no projeto aos workloads.
Quando `enable_technical_dead_letter=false`, os dois bindings do Pub/Sub service
agent nao sao criados. Os bindings do Ledger Worker e do Balance Worker
permanecem inalterados.

O root module dev garante previamente a identidade gerenciada do Pub/Sub com
`google_project_service_identity.pubsub`. Os bindings IAM usam o atributo
`member` retornado pelo recurso, sem montar o e-mail a partir do numero do
projeto.

`service_account_token_creator_members` usa lista vazia por padrao. Valores
reais devem existir somente no `terraform.tfvars` local ignorado pelo Git. Apos
o smoke test, limpe a lista e reaplique Terraform manualmente para remover os
bindings temporarios.

## Nomes esperados em dev

| Recurso | Nome |
| --- | --- |
| Projeto GCP | valor informado em `project_id` |
| Topic principal | `ledger.ledgerentry.created.dev` |
| Subscription do Balance | `balance-service-ledger-events-dev` |
| Topic da DLQ de aplicacao | `ledger.ledgerentry.created.dlq.dev` |
| Subscription de inspecao da DLQ de aplicacao | `balance-service-ledger-events-dlq-dev` |
| Topic da DLQ tecnica | `ledger.ledgerentry.created.technical.dlq.dev` |
| Subscription de inspecao da DLQ tecnica | `balance-service-ledger-events-technical-dlq-dev` |

O topic compartilhado e sua subscription de inspecao existentes sao preservados
como DLQ de aplicacao por `moved` blocks do Terraform. A DLQ tecnica e criada
separadamente para os novos encaminhamentos da dead-letter policy nativa.
O topic e a subscription de inspecao da DLQ tecnica continuam criados quando
`enable_technical_dead_letter=false`; somente a policy nativa e seu IAM
especifico sao omitidos. Essa escolha preserva outputs estaveis e simplifica a
ativacao posterior durante rollout incremental.

## Checklist antes de usar GCP real

- Confirmar projeto dev e identidade autenticada antes de `terraform plan`.
- Para validacao sintatica sem credenciais, executar `terraform fmt -check -recursive`, `terraform init -backend=false` e `terraform validate`.
- Para `terraform plan` real, inicializar antes o backend remoto com `terraform init -backend-config="bucket=<terraform-state-bucket>"` e nao usar `-lock=false`.
- Revisar e executar `terraform apply` manualmente somente apos autorizacao.
- Obter `terraform output -json` e preencher as options conforme a tabela.
- Configurar a service account dedicada de cada worker sem chave JSON versionada.
- Remover `PUBSUB_EMULATOR_HOST` do ambiente dos workers.
- Confirmar `enable_message_ordering=true` no producer e na subscription dev.
- Confirmar `enable_technical_dead_letter` conforme a fase do rollout.
- Confirmar `allowed_persistence_regions=[]` em dev enquanto nao houver decisao de residencia.
- Para ambiente real com residencia aprovada em Sao Paulo, configurar `allowed_persistence_regions=["southamerica-east1"]`.
- Revisar localizacao dos workloads antes de habilitar `enforce_in_transit=true`, pois requests fora das regioes permitidas podem ser rejeitados.
- Confirmar `message_retention_duration="604800s"` e `retain_acked_messages=false`.
- Confirmar `main_subscription_expiration_ttl=""` para preservar a subscription principal.
- Revisar se os TTLs finitos das inspections de DLQ sao maiores que a retencao e adequados ao ambiente.
- Quando a policy tecnica estiver habilitada, confirmar o IAM do Pub/Sub service agent.
- Em projeto novo, revisar no primeiro `terraform plan` a criacao de `google_project_service_identity.pubsub` antes dos bindings IAM.
- Confirmar que `PubSub:Consumer:DeadLetterTopicId` usa `application_dlq_topic_name`; a DLQ tecnica nao e configurada na aplicacao.
- Publicar um evento de teste e validar consumo, projecao e inspecao da DLQ.
