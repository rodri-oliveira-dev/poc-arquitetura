# Operacao do Pub/Sub

Este runbook descreve como selecionar, executar e diagnosticar o provider Pub/Sub no fluxo de eventos financeiros entre `LedgerService.Worker` e `BalanceService.Worker`.

Kafka continua sendo o provider padrao. Pub/Sub e uma alternativa incremental: preserve Outbox, idempotencia e a separacao entre DLQ de aplicacao e DLQ tecnica do transporte.

## Escolher o provider

Os workers aceitam os valores `Kafka` e `PubSub` em `Messaging:Provider`. Qualquer outro valor falha no startup.

Em `appsettings*.json`:

```json
{
  "Messaging": {
    "Provider": "Kafka"
  }
}
```

Para usar Pub/Sub:

```json
{
  "Messaging": {
    "Provider": "PubSub"
  }
}
```

Com variaveis de ambiente, use `__` para separar secoes:

```powershell
$env:Messaging__Provider = "Kafka"
```

ou:

```powershell
$env:Messaging__Provider = "PubSub"
```

`Kafka:Enabled=false` e `PubSub:Enabled=false` desligam os hosted services do provider correspondente em testes ou cenarios locais especificos.

## Pub/Sub emulator local

O emulator e descartavel, nao usa credenciais GCP e fica fora do Terraform. Ele nao reproduz integralmente limites e garantias do servico real.

No Windows:

```powershell
./scripts/start-local-stack-pubsub.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack-pubsub.sh
```

Os scripts aplicam o overlay `compose.pubsub.yaml`, executam migrations e iniciam APIs e workers. O overlay:

- sobe `pubsub-emulator` em `localhost:8085` por padrao;
- cria idempotentemente topic principal, topic de DLQ e subscription pull do Balance;
- configura os workers com `Messaging__Provider=PubSub`;
- mantem Kafka disponivel para comparacao entre providers.

O overlay local nao configura a dead-letter policy nativa da subscription. Esse comportamento tecnico pertence aos recursos reais provisionados pelo Terraform.

Defaults locais:

| Item | Valor |
| --- | --- |
| Projeto do emulator | `poc-local` |
| Topic principal | `ledger.ledgerentry.created.local` |
| Topic de DLQ | `ledger.ledgerentry.created.dlq.local` |
| Subscription do Balance | `balance-service-ledger-events-local` |

Copie `.env.example` para `.env` quando precisar sobrescrever `PUBSUB_EMULATOR_HOST_PORT`, `PUBSUB_PROJECT_ID`, `PUBSUB_LEDGER_EVENTS_TOPIC_ID`, `PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID` ou `PUBSUB_BALANCE_SUBSCRIPTION_ID`. Nao versione `.env`.

Para inspecionar a configuracao efetiva e os logs:

```bash
docker compose -f compose.yaml -f compose.pubsub.yaml config
docker compose -f compose.yaml -f compose.pubsub.yaml logs pubsub-emulator pubsub-init ledger-worker balance-worker
```

## Configurar os workers

Os perfis locais versionados ficam em:

- `src/LedgerService.Worker/appsettings.PubSub.json`;
- `src/BalanceService.Worker/appsettings.PubSub.json`.

Para rodar os workers no host contra o emulator ja iniciado, abra um terminal para cada processo:

```powershell
$env:DOTNET_ENVIRONMENT = "PubSub"
$env:PUBSUB_EMULATOR_HOST = "127.0.0.1:8085"
$env:PUBSUB_PROJECT_ID = "poc-local"
dotnet run --project ./src/LedgerService.Worker
```

```powershell
$env:DOTNET_ENVIRONMENT = "PubSub"
$env:PUBSUB_EMULATOR_HOST = "127.0.0.1:8085"
$env:PUBSUB_PROJECT_ID = "poc-local"
dotnet run --project ./src/BalanceService.Worker
```

Para configurar sem perfil `appsettings.PubSub.json`, use os outputs do Terraform e sobrescreva explicitamente:

| Worker | Variavel | Finalidade |
| --- | --- | --- |
| Ledger | `Messaging__Provider=PubSub` | Seleciona o adapter Pub/Sub. |
| Ledger | `PubSub__Producer__ProjectId` | Projeto GCP. |
| Ledger | `PubSub__Producer__DefaultTopicId` | Topic principal para eventos financeiros. |
| Ledger | `PubSub__Producer__EnableMessageOrdering` | Habilita ordering key quando o fluxo exigir ordenacao por agregado. |
| Balance | `Messaging__Provider=PubSub` | Seleciona o adapter Pub/Sub. |
| Balance | `PubSub__Consumer__ProjectId` | Projeto GCP. |
| Balance | `PubSub__Consumer__SubscriptionId` | Subscription pull consumida pelo Balance. |
| Balance | `PubSub__Consumer__DeadLetterTopicId` | Topic usado pela DLQ de aplicacao. |

`PUBSUB_EMULATOR_HOST` deve existir apenas quando o processo aponta para o emulator. Fora do ambiente local, use a identidade do workload e nao configure credenciais no repositorio.

## Aplicar Terraform em dev

O root module `infra/terraform/environments/dev` provisiona recursos reais na GCP: API Pub/Sub, topics, subscriptions, service accounts dedicadas e bindings IAM de menor privilegio. O state ainda e local; nao versione `terraform.tfvars`, `tfplan`, state ou credenciais.

Antes do apply, confirme a identidade autenticada, o projeto dev alvo e a permissao para habilitar servicos e gerenciar Pub/Sub, service accounts e IAM dos recursos. Prefira ADC com impersonation em vez de chave JSON.

No PowerShell:

```powershell
Set-Location ./infra/terraform/environments/dev
Copy-Item terraform.tfvars.example terraform.tfvars
```

Edite somente o arquivo local `terraform.tfvars`, substitua o placeholder de `project_id` e revise `region`. Depois valide, gere um plano revisavel e aplique manualmente:

```powershell
terraform fmt -check
terraform init
terraform validate
terraform plan -out=tfplan
terraform apply tfplan
terraform output
```

O apply habilita `pubsub.googleapis.com` e pode gerar custo no projeto informado. Nao execute `terraform apply` automaticamente e nao use esse fluxo contra o emulator.

Outputs relevantes para configurar os workers:

- `project_id`;
- `ledger_events_topic_name`;
- `ledger_events_subscription_name`;
- `ledger_events_dlq_topic_name`;
- `ledger_events_dlq_subscription_name`;
- `ledger_worker_service_account_email`;
- `balance_worker_service_account_email`.

## Fluxo operacional

1. `LedgerService.Api` persiste a operacao e grava uma mensagem em `outbox_messages` na mesma transacao.
2. `OutboxPublisherService`, hospedado no `LedgerService.Worker`, reclama mensagens pendentes e publica no provider selecionado.
3. Com Pub/Sub, o evento financeiro segue para o topic principal e o `BalanceService.Worker` o recebe pela subscription pull configurada.
4. O Balance valida o contrato e atualiza a projecao de saldo de forma idempotente.
5. Mensagens invalidas, contratos nao suportados ou falhas classificadas como nao recuperaveis sao publicadas pela aplicacao no topic de DLQ.
6. Falhas tecnicas de entrega podem ser encaminhadas pela dead-letter policy nativa da subscription para a DLQ tecnica do Pub/Sub.

O modulo Terraform atual usa um unico topic de DLQ para encaminhamento tecnico e publicacao classificada pela aplicacao. A origem deve permanecer distinguivel pelos attributes e pelo envelope da mensagem.

## Kafka e Pub/Sub

| Kafka | Pub/Sub |
| --- | --- |
| Producer publica em topic; consumer le topics por grupo. | Producer publica em topic; consumer recebe por subscription. |
| Metadados de transporte usam headers. | Metadados de transporte usam attributes. |
| Consumer controla commit de offset. | Consumer responde com `ack` ou `nack`. |
| Possui partition e offset. | Nao possui partition nem offset. |
| Message key influencia particionamento e ordenacao dentro da partition. | Ordering key pode preservar ordem para mensagens com a mesma chave quando habilitada, mas nao e partition. |

Nao simule partition, offset ou commit no adapter Pub/Sub. Os processors neutros devem depender apenas dos conceitos compartilhados pelo boundary.

## Troubleshooting

Use esta ordem para reduzir diagnosticos ambiguos:

- **Sem permissao de publish:** confirme se o `LedgerService.Worker` usa a service account esperada e se ela possui `roles/pubsub.publisher` no topic principal. Verifique logs do `ledger-worker` e o estado da Outbox.
- **Sem permissao de subscriber:** confirme se o `BalanceService.Worker` usa a service account esperada e se ela possui `roles/pubsub.subscriber` na subscription principal.
- **Subscription sem mensagens:** confirme topic e subscription configurados, valide se a subscription aponta para o topic principal e verifique se a Outbox saiu de `Pending` para `Processed`.
- **Emulator nao configurado:** confirme `PUBSUB_EMULATOR_HOST=127.0.0.1:8085` no host ou `pubsub-emulator:8085` nos containers. Inspecione `pubsub-emulator` e `pubsub-init`.
- **DLQ sem IAM do service agent:** confirme se o service agent `service-{project_number}@gcp-sa-pubsub.iam.gserviceaccount.com` possui `roles/pubsub.publisher` no topic de DLQ e `roles/pubsub.subscriber` na subscription principal.
- **Mensagem duplicada:** trate como possibilidade esperada do fluxo at-least-once. Confirme a idempotencia do Balance pela identidade do evento antes de tentar republicar ou remover mensagens.

Para detalhes complementares, consulte [Kafka, Outbox e DLQ](../development/kafka-outbox.md), [desenvolvimento local](../development/local-development.md#pubsub-emulator-local), [setup local Terraform e GCP](../development/terraform-gcp-local-setup.md) e [ADR-0077](../adrs/0077-pubsub-provider-mensageria.md).
