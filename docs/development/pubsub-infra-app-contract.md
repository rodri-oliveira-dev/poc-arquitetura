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
| `project_id` | `PubSub:Producer:ProjectId` | Projeto GCP do `LedgerService.Worker`. |
| `ledger_events_topic_name` | `PubSub:Producer:DefaultTopicId` | Topic ID simples: `ledger.ledgerentry.created.dev`. |
| `ledger_events_topic_map` | `PubSub:Producer:TopicMap` | Mapeia `LedgerEntryCreated.v1` para o topic principal. |
| `enable_message_ordering` | `PubSub:Producer:EnableMessageOrdering` | Deve permanecer alinhado com a subscription. |
| `project_id` | `PubSub:Consumer:ProjectId` | Projeto GCP do `BalanceService.Worker`. |
| `ledger_events_subscription_name` | `PubSub:Consumer:SubscriptionId` | Subscription ID simples consumida pelo Balance. |
| `ledger_events_dlq_topic_name` | `PubSub:Consumer:DeadLetterTopicId` | Topic ID simples usado pela DLQ de aplicacao. |
| `enable_exactly_once_delivery` | `PubSub:Consumer:EnableExactlyOnceDelivery` | Espelha a configuracao da subscription. |
| `ack_deadline_seconds` | `PubSub:Consumer:AckDeadlineSeconds` | Espelha a configuracao da subscription. |

`PubSub:Consumer:ProcessingErrorRetryDelay` e uma option local do worker, com
default `00:00:05`; nao possui output Terraform equivalente. O adapter responde
`nack` em falhas recuperaveis e a retry policy nativa da subscription governa o
redelivery. `Messaging:Provider=PubSub` tambem deve ser definido nos dois
workers.

## IAM minimo

| Service account | Permissao | Motivo |
| --- | --- | --- |
| Ledger Worker | `roles/pubsub.publisher` no topic principal | Publicar eventos da Outbox. |
| Balance Worker | `roles/pubsub.subscriber` na subscription principal | Consumir e confirmar eventos financeiros. |
| Balance Worker | `roles/pubsub.publisher` no topic de DLQ | Publicar mensagens classificadas pela aplicacao. |
| Pub/Sub service agent | `roles/pubsub.publisher` no topic de DLQ | Encaminhar mensagens pela dead-letter policy nativa. |
| Pub/Sub service agent | `roles/pubsub.subscriber` na subscription principal | Confirmar mensagens encaminhadas pela dead-letter policy nativa. |

O modulo nao concede `Owner`, `Editor` nem `Viewer` no projeto aos workloads.

## Nomes esperados em dev

| Recurso | Nome |
| --- | --- |
| Projeto GCP | valor informado em `project_id` |
| Topic principal | `ledger.ledgerentry.created.dev` |
| Subscription do Balance | `balance-service-ledger-events-dev` |
| Topic de DLQ tecnica e de aplicacao | `ledger.ledgerentry.created.dlq.dev` |
| Subscription de inspecao da DLQ | `balance-service-ledger-events-dlq-dev` |

## Checklist antes de usar GCP real

- Confirmar projeto dev e identidade autenticada antes de `terraform plan`.
- Executar `terraform fmt -check -recursive`, `terraform init -backend=false` e `terraform validate`.
- Revisar e executar `terraform apply` manualmente somente apos autorizacao.
- Obter `terraform output -json` e preencher as options conforme a tabela.
- Configurar a service account dedicada de cada worker sem chave JSON versionada.
- Remover `PUBSUB_EMULATOR_HOST` do ambiente dos workers.
- Confirmar `enable_message_ordering=true` no producer e na subscription dev.
- Confirmar o IAM do Pub/Sub service agent para a dead-letter policy.
- Publicar um evento de teste e validar consumo, projecao e inspecao da DLQ.
