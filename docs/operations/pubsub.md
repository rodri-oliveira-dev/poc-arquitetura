# Operacao do Pub/Sub

Este runbook descreve como selecionar, executar e diagnosticar o provider Pub/Sub no fluxo de eventos financeiros entre `LedgerService.Worker` e `BalanceService.Worker`.

Pub/Sub e um provider explicito/legado. Kafka e o default local dos workers principais; preserve Outbox, idempotencia e a separacao entre DLQ de aplicacao e DLQ tecnica do transporte ao alternar providers.

Nao use Pub/Sub para novos fluxos padrao deste repositorio sem decisao arquitetural nova. O modo Pub/Sub continua util para validar o adapter existente, estudar o emulator ou executar smoke manual contra recursos GCP dev ja provisionados.

## Escolher o provider

Os workers aceitam os valores `Kafka` e `PubSub` em `Messaging:Provider`. Qualquer outro valor falha no startup.

O default versionado e:

```json
{
  "Messaging": {
    "Provider": "Kafka"
  }
}
```

Para usar Pub/Sub explicitamente:

```json
{
  "Messaging": {
    "Provider": "PubSub"
  }
}
```

Com variaveis de ambiente, use `__` para separar secoes:

```powershell
$env:Messaging__Provider = "PubSub"
```

ou:

```powershell
$env:Messaging__Provider = "Kafka"
```

`Kafka:Enabled=false` e `PubSub:Enabled=false` desligam os hosted services do provider correspondente em testes ou cenarios locais especificos.

## Pub/Sub emulator local

O emulator e descartavel, nao usa credenciais GCP e fica fora do Terraform. Ele nao reproduz integralmente limites e garantias do servico real.

Use os scripts explicitos de Pub/Sub. No Windows:

```powershell
./scripts/local/start-stack-pubsub.ps1
```

No Linux/macOS:

```bash
./scripts/local/start-stack-pubsub.sh
```

Os scripts usam `compose.yaml` com `compose.pubsub.yaml`, executam migrations e iniciam APIs e workers de Ledger/Balance. O compose:

- sobe `pubsub-emulator` em `localhost:8085` por padrao;
- cria idempotentemente topic principal, topic de DLQ, subscription pull do Balance e subscription de inspecao da DLQ de aplicacao;
- configura os workers com `Messaging__Provider=PubSub`;
- nao inicia Kafka.

O overlay local nao configura a dead-letter policy nativa da subscription. Esse comportamento tecnico pertence aos recursos reais provisionados pelo Terraform.

Defaults locais:

| Item | Valor |
| --- | --- |
| Projeto do emulator | `poc-local` |
| Topic principal | `ledger.ledgerentry.created.local` |
| Topic de DLQ | `ledger.ledgerentry.created.dlq.local` |
| Subscription do Balance | `balance-service-ledger-events-local` |
| Subscription de inspecao da DLQ de aplicacao | `ledger-events-application-dlq-inspection-local` |

Copie `.env.local.example` para `.env.local` quando precisar sobrescrever `PUBSUB_EMULATOR_HOST_PORT`, `PUBSUB_PROJECT_ID`, `PUBSUB_LEDGER_EVENTS_TOPIC_ID`, `PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID`, `PUBSUB_BALANCE_SUBSCRIPTION_ID` ou `PUBSUB_LEDGER_EVENTS_DLQ_INSPECTION_SUBSCRIPTION_ID`. Nao versione `.env.local`.

Para inspecionar a configuracao efetiva e os logs:

```bash
docker compose --env-file .env.local -f compose.yaml -f compose.pubsub.yaml --profile legacy-pubsub config
docker compose --env-file .env.local -f compose.yaml -f compose.pubsub.yaml --profile legacy-pubsub logs pubsub-emulator pubsub-init ledger-worker balance-worker
```

## Configurar os workers

Os defaults locais versionados ficam em `appsettings.json`; os perfis
`appsettings.PubSub.json` permanecem como exemplos explicitos:

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
| Ledger | `PubSub__Producer__TopicMap__LedgerEntryCreated.v1` | Mapeia explicitamente o contrato legado para o topic principal. |
| Ledger | `PubSub__Producer__TopicMap__LedgerEntryCreated.v2` | Mapeia explicitamente o contrato atual para o topic principal. |
| Ledger | `PubSub__Producer__EnableMessageOrdering` | Habilita ordering key quando o fluxo exigir ordenacao por agregado. |
| Balance | `Messaging__Provider=PubSub` | Seleciona o adapter Pub/Sub. |
| Balance | `PubSub__Consumer__ProjectId` | Projeto GCP. |
| Balance | `PubSub__Consumer__SubscriptionId` | Subscription pull consumida pelo Balance. |
| Balance | `PubSub__Consumer__DeadLetterTopicId` | Topic usado pela DLQ de aplicacao. |
| Balance | `PubSub__Consumer__EnableExactlyOnceDelivery` | Espelha a configuracao da subscription provisionada. |
| Balance | `PubSub__Consumer__AckDeadlineSeconds` | Espelha o ack deadline da subscription provisionada. |
| Balance | `PubSub__Consumer__ProcessingErrorRetryDelay` | Valor validado pelo worker; o redelivery Pub/Sub continua governado pela retry policy nativa. |

`PUBSUB_EMULATOR_HOST` deve existir apenas quando o processo aponta para o emulator. Fora do ambiente local, use a identidade do workload e nao configure credenciais no repositorio.

Os perfis `appsettings.PubSub.json` usam nomes `*.local` exclusivamente para o
emulator. Para GCP real, use os outputs `*.dev` do Terraform conforme o
[contrato entre infraestrutura e aplicacao](../development/pubsub-infra-app-contract.md).
Remova `PUBSUB_EMULATOR_HOST` do processo antes de apontar para GCP real.

## Kafka default

Para iniciar Kafka no local:

```powershell
./scripts/local/start-stack-kafka.ps1
```

```bash
./scripts/local/start-stack-kafka.sh
```

Esse fluxo chama o compose principal com `Messaging__Provider=Kafka`, mas `./scripts/local/start-stack.ps1` e `./scripts/local/start-stack.sh` ja usam Kafka por padrao.

## Aplicar Terraform em dev

O root module `infra/terraform/environments/dev` provisiona recursos reais na GCP: API Pub/Sub, service identity gerenciada, topics, subscriptions, service accounts dedicadas e bindings IAM de menor privilegio. O backend remoto GCS usa o bucket `rodri-terraform-state-bucket` e o prefixo `poc-arquitetura/pubsub/dev`. Nao versione `terraform.tfvars`, `tfplan`, state ou credenciais.

O bucket de state deve ser dedicado, versionado e acessivel apenas a operadores
Terraform autorizados, administradores de bootstrap/auditoria e identidade de
CI/CD se um fluxo futuro executar `terraform plan` real. Service accounts dos
workloads nao devem acessar o bucket de state. A [ADR-0080](../adrs/0080-backend-remoto-gcs-terraform-dev.md)
registra a decisao de backend remoto GCS para dev.

Antes do apply, confirme a identidade autenticada, o projeto dev alvo e a permissao para habilitar servicos e gerenciar Pub/Sub, service accounts e IAM dos recursos. Prefira ADC com impersonation em vez de chave JSON.

No PowerShell:

```powershell
Set-Location ./infra/terraform/environments/dev
Copy-Item terraform.tfvars.example terraform.tfvars
```

Edite somente o arquivo local `terraform.tfvars`, substitua o placeholder de `project_id` e revise `region`. Depois valide, gere um plano revisavel e aplique manualmente:

```powershell
terraform fmt -check
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform validate
terraform plan -out=tfplan
terraform apply tfplan
terraform output
```

O apply habilita `pubsub.googleapis.com` e pode gerar custo no projeto informado. Nao execute `terraform apply` automaticamente e nao use esse fluxo contra o emulator. Nao use `-lock=false`; o backend GCS deve proteger concorrencia durante `plan` e `apply`.

Se existir state local anterior, faca backup e migre manualmente antes do
primeiro plan operacional com backend remoto:

```powershell
Copy-Item terraform.tfstate terraform.tfstate.pre-gcs-migration.backup
terraform init -migrate-state -backend-config="bucket=rodri-terraform-state-bucket"
terraform state list
terraform plan -var-file="terraform.tfvars"
```

Nao versionar o backup, arquivos de state, planos binarios ou credenciais.

### Impersonation local para smoke test

Para executar os workers localmente contra GCP real com ADC impersonation,
preencha `service_account_token_creator_members` somente no
`terraform.tfvars` local ignorado pelo Git. O modulo concede
`roles/iam.serviceAccountTokenCreator` diretamente nas duas service accounts
dedicadas dos workers; nao existe binding no nivel do projeto. Essa permissao
serve apenas ao smoke test local controlado.

Depois do smoke test, limpe a lista e reaplique Terraform manualmente para
remover os bindings temporarios.

Como o ADC local e global para o ambiente do usuario, execute o smoke test
contra GCP real sequencialmente:

1. configure ADC impersonando a service account do `LedgerService.Worker`;
2. inicie somente o Ledger Worker e aguarde a Outbox ser marcada como
   `Processed`;
3. pare o Ledger Worker;
4. configure ADC impersonando a service account do `BalanceService.Worker`;
5. inicie somente o Balance Worker e valide `processed_events`,
   `daily_balances` e a API de consulta;
6. pare o Balance Worker;
7. restaure ADC humano antes de executar `terraform plan`, `terraform apply`
   ou `terraform destroy`.

As service accounts dedicadas dos workers possuem somente permissoes runtime.
Usar ADC impersonado de worker para administrar Terraform deve falhar por
permissao e nao deve ser contornado ampliando IAM do workload.

Em GitHub Actions, passe o valor por variable:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ vars.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

ou por secret:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ secrets.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

Para um CI/CD mais maduro, evolua para Workload Identity Federation com OIDC em
vez de usar e-mail humano.

Em projetos novos, revise no primeiro `terraform plan` a criacao de
`google_project_service_identity.pubsub`. O recurso usa `hashicorp/google-beta`
porque `google_project_service_identity` permanece beta no provider e garante a
identidade antes dos bindings IAM exclusivos da DLQ tecnica.

O exemplo dev mantem `allowed_persistence_regions=[]` e
`enforce_in_transit=false`. Assim, os topics nao recebem
`message_storage_policy` explicita. O valor de `region` continua sendo metadado
do ambiente e label dos recursos; isoladamente, ele nao restringe onde o
Pub/Sub armazena ou processa mensagens.

Outputs relevantes para configurar os workers:

- `project_id`;
- `ledger_events_topic_name`;
- `ledger_events_topic_map`;
- `ledger_events_subscription_name`;
- `application_dlq_topic_name`;
- `application_dlq_subscription_name`;
- `technical_dlq_topic_name`;
- `technical_dlq_subscription_name`;
- `enable_technical_dead_letter`;
- `enable_message_ordering`;
- `enable_exactly_once_delivery`;
- `ack_deadline_seconds`;
- `message_retention_duration`;
- `retain_acked_messages`;
- `main_subscription_expiration_ttl`;
- `application_dlq_subscription_expiration_ttl`;
- `technical_dlq_subscription_expiration_ttl`;
- `ledger_worker_service_account_email`;
- `balance_worker_service_account_email`.

## Fluxo operacional

1. `LedgerService.Api` persiste a operacao e grava uma mensagem em `outbox_messages` na mesma transacao.
2. `OutboxPublisherService`, hospedado no `LedgerService.Worker`, reclama mensagens pendentes e publica no provider selecionado.
3. Com Pub/Sub, o evento financeiro segue para o topic principal e o `BalanceService.Worker` o recebe pela subscription pull configurada.
4. O Balance valida o contrato e atualiza a projecao de saldo de forma idempotente.
5. Mensagens invalidas, contratos nao suportados ou falhas classificadas como nao recuperaveis sao publicadas pela aplicacao no topic de DLQ.
6. Falhas tecnicas de entrega podem ser encaminhadas pela dead-letter policy nativa da subscription para a DLQ tecnica do Pub/Sub.

O modulo Terraform usa topics e subscriptions de inspecao separados para a DLQ de aplicacao e a DLQ tecnica. Configure `PubSub:Consumer:DeadLetterTopicId` somente com o output `application_dlq_topic_name`; a DLQ tecnica pertence exclusivamente a dead-letter policy nativa.

Para rollout incremental ou testes em dev que nao precisem do encaminhamento
nativo, defina `enable_technical_dead_letter=false` no `terraform.tfvars`. A
subscription principal continua criada e o `BalanceService.Worker` continua
consumindo mensagens normalmente. A DLQ de aplicacao tambem continua criada e
publicada pelo worker. O topic e a subscription de inspecao da DLQ tecnica
permanecem provisionados para simplificar a ativacao posterior, mas a policy
nativa e os dois bindings IAM exclusivos do Pub/Sub service agent sao omitidos.

## Residencia, seguranca e transferencia entre regioes

O modulo Terraform aplica a mesma `message_storage_policy` opcional ao topic
principal, ao topic da DLQ de aplicacao e ao topic da DLQ tecnica. Dev nao
restringe residencia por padrao porque ainda nao existe decisao de ambiente que
justifique fixar uma regiao.

Quando um ambiente real aprovar residencia em Sao Paulo, configure:

```hcl
allowed_persistence_regions = ["southamerica-east1"]
enforce_in_transit          = false
```

Com a lista preenchida, o Pub/Sub armazena e processa o conteudo das mensagens
somente nas regioes permitidas. Publishers e subscribers podem continuar fora
dessas regioes quando `enforce_in_transit=false`, mas esse roteamento pode
atravessar fronteiras regionais e gerar transferencia cobrada.

Use `enforce_in_transit=true` somente apos revisar localizacao dos workloads,
endpoints e cenarios operacionais. Nesse modo, Pub/Sub pode rejeitar publish,
pull e streamingPull recebidos fora das regioes permitidas.

Referencias oficiais:

- [Configure message storage policies](https://cloud.google.com/pubsub/docs/resource-location-restriction)
- [Pub/Sub pricing](https://cloud.google.com/pubsub/pricing)

## Retencao, expiracao, custo e free tier

As tres subscriptions provisionadas pelo Terraform declaram
`expiration_policy` explicitamente:

| Subscription | Default em dev | Motivo |
| --- | --- | --- |
| Principal do Balance | `ttl = ""` | Nao expirar por inatividade e preservar o backlog do consumidor. |
| Inspecao da DLQ de aplicacao | `ttl = "2592000s"` | Expirar apos 30 dias sem atividade em dev descartavel. |
| Inspecao da DLQ tecnica | `ttl = "2592000s"` | Expirar apos 30 dias sem atividade em dev descartavel. |

O provider Terraform representa "nunca expirar" com o bloco
`expiration_policy` presente e `ttl = ""`. Em ambientes permanentes, defina
tambem `application_dlq_subscription_expiration_ttl=""` e
`technical_dlq_subscription_expiration_ttl=""` quando as subscriptions de
inspecao precisarem sobreviver a longos periodos sem uso.

Todas as subscriptions mantem mensagens nao confirmadas por sete dias
(`message_retention_duration = "604800s"`) e nao mantem mensagens confirmadas
(`retain_acked_messages = false`). Um TTL de expiracao finito deve ser maior que
a retencao. Backlog nao processado e DLQ acumulada durante a janela de retencao
podem gerar custo de armazenamento no Pub/Sub; acompanhe crescimento e descarte
ou reprocesse mensagens conforme o procedimento operacional aplicavel.

O free tier mensal de throughput nao deve ser tratado como garantia de custo
zero. Alem de throughput acima da franquia aplicavel, retencao e transferencias
entre regioes podem gerar cobranca. Uma `message_storage_policy` pode aumentar o
custo de transferencia se obrigar a mensagem a sair da regiao do publisher ou
for entregue a subscribers em outra regiao.

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
- **DLQ tecnica sem IAM do service agent:** confirme se o `member` retornado por `google_project_service_identity.pubsub` possui `roles/pubsub.publisher` no topic da DLQ tecnica e `roles/pubsub.subscriber` na subscription principal.
- **DLQ de aplicacao sem publish:** confirme se a service account do `BalanceService.Worker` possui `roles/pubsub.publisher` somente no topic da DLQ de aplicacao e se `PubSub:Consumer:DeadLetterTopicId` aponta para `application_dlq_topic_name`.
- **Mensagem duplicada:** trate como possibilidade esperada do fluxo at-least-once. Confirme a idempotencia do Balance pela identidade do evento antes de tentar republicar ou remover mensagens.

Para detalhes complementares, consulte [Kafka, Outbox e DLQ](../development/kafka-outbox.md), [desenvolvimento local](../development/local-development.md#pubsub-emulator-local), [setup local Terraform e GCP](../development/terraform-gcp-local-setup.md), [ADR-0088](../adrs/0088-kafka-default-ledger-balance-workers.md) e o historico substituido na [ADR-0078](../adrs/0078-pubsub-provider-principal-local-emulator.md).
