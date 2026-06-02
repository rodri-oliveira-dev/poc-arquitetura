# ADR-0077: Pub/Sub como provider alternativo de mensageria

## Status
Substituido pela [ADR-0078](0078-pubsub-provider-principal-local-emulator.md)

## Data
2026-06-01

## Contexto
O projeto usa Kafka como provider atual para publicar eventos do `LedgerService` e consumi-los no `BalanceService`, com Outbox, DLQ e consumidores idempotentes. A ADR-0075 definiu o boundary de mensageria por Ports and Adapters e registrou Pub/Sub como adapter futuro possivel, sem criar uma abstracao falsa que tente igualar providers com semanticas diferentes.

Para permitir uma migracao gradual para Google Cloud Pub/Sub, o projeto precisa registrar como esse novo adapter sera introduzido sem substituir Kafka imediatamente e sem perder as garantias atuais de publicacao e consumo.

## Decisao
Implementar Google Cloud Pub/Sub de forma incremental como provider alternativo de mensageria:

- Kafka permanece funcionando como provider atual.
- A configuracao `Messaging:Provider` aceita `PubSub` quando o adapter correspondente esta configurado.
- O adapter Pub/Sub sera completado nas composition roots e nos boundaries definidos pela ADR-0075, sem substituir Kafka de forma imediata.
- A Outbox sera preservada para publicacao confiavel.
- Os consumidores continuarao idempotentes para tratar entregas repetidas.
- Metadados de correlacao, rastreamento, tipo de evento e idempotencia serao transportados por attributes do Pub/Sub no lugar de headers Kafka.
- A ordering key sera baseada no `AggregateId` quando o fluxo exigir ordenacao por agregado.

O adapter Pub/Sub deve modelar explicitamente as semanticas do provider:

- `ack` e `nack`;
- delivery attempts;
- ordering keys;
- dead-letter topics e dead-letter subscriptions;
- attributes;
- limites operacionais do provider.

Pub/Sub nao possui partition, offset nem commit. Esses conceitos permanecem restritos ao adapter Kafka e nao devem ser simulados no adapter Pub/Sub nem expostos aos processors neutros.

## DLQ tecnica e DLQ de aplicacao
A dead-letter policy nativa do Pub/Sub sera tratada como DLQ tecnica do transporte. Ela cobre falhas de entrega conforme as tentativas configuradas na subscription.

A DLQ de aplicacao permanece distinta e continua cobrindo mensagens invalidas, contratos nao suportados e falhas classificadas pela aplicacao como nao recuperaveis. A implementacao futura deve preservar essa separacao operacional e tornar observavel a origem de cada mensagem encaminhada para DLQ.

O Terraform permite desligar a policy tecnica nativa por ambiente com `enable_technical_dead_letter=false`, sem remover a subscription principal nem a DLQ de aplicacao publicada pelo `BalanceService.Worker`. O topic e a subscription de inspecao da DLQ tecnica permanecem provisionados para preservar outputs estaveis e simplificar a ativacao posterior. Quando a policy esta desligada, os bindings IAM do Pub/Sub service agent exclusivos desse fluxo tecnico nao sao criados.

As subscriptions declaram `expiration_policy` explicitamente. A subscription
principal nao expira por inatividade. Em dev, as subscriptions de inspecao das
DLQs expiram apos 30 dias sem atividade; ambientes permanentes podem configura-las
para nao expirar. Todas mantem a retencao de mensagens nao confirmadas em sete
dias e nao retem mensagens confirmadas. TTLs de expiracao finitos devem ser
maiores que a janela de retencao.

Os tres topics Terraform aceitam `message_storage_policy` opcional por ambiente:
topic principal, DLQ de aplicacao e DLQ tecnica. Em dev,
`allowed_persistence_regions=[]` omite a policy porque ainda nao existe decisao
de residencia regional. O valor `region` permanece metadado e label; nao
restringe sozinho onde mensagens sao armazenadas ou processadas. Ambientes reais
podem preencher `allowed_persistence_regions`, por exemplo com
`["southamerica-east1"]`, apos decisao explicita.

## Provisionamento e desenvolvimento local
Os recursos reais do Pub/Sub na GCP serao provisionados com Terraform, incluindo topics, subscriptions, dead-letter topics, dead-letter subscriptions e configuracoes necessarias ao provider.

Como primeira entrega operacional, o desenvolvimento local usa o Pub/Sub emulator pelo overlay `compose.pubsub.yaml` e pelos scripts `scripts/start-local-stack-pubsub.*`. O emulator fica fora do Terraform: sua inicializacao e configuracao pertencem ao setup local da POC, sem representar o provisionamento real da GCP.

## Consequencias

### Beneficios
- A migracao pode ocorrer gradualmente, mantendo Kafka operacional.
- Outbox e idempotencia continuam protegendo os fluxos assincronos.
- As diferencas semanticas entre Kafka e Pub/Sub ficam explicitas nos adapters.
- Terraform registra o provisionamento reproduzivel dos recursos reais da GCP.

### Trade-offs / riscos
- Durante a migracao, o projeto precisara operar e testar dois providers.
- Configuracoes, metricas, observabilidade e procedimentos operacionais devem distinguir Kafka, Pub/Sub, DLQ tecnica e DLQ de aplicacao.
- A policy tecnica pode ser desligada em rollouts incrementais e testes de dev, mas mensagens com falha de entrega deixam de ser encaminhadas automaticamente pelo transporte enquanto a flag estiver desabilitada.
- Backlogs nao processados e DLQs acumuladas durante a janela de retencao podem gerar custo de armazenamento no Pub/Sub.
- Uma `message_storage_policy` pode gerar transferencia cobrada entre regioes quando o armazenamento ou a entrega atravessar fronteiras regionais.
- `enforce_in_transit=true` exige revisao cuidadosa das regioes e endpoints dos workloads porque requests de publish, pull e streamingPull fora das regioes permitidas podem ser rejeitados.
- O emulator local nao reproduz integralmente o comportamento e os limites do servico real na GCP.
- Ordering keys devem ser habilitadas apenas nos fluxos que realmente exigirem ordenacao por agregado.

## Fora do escopo
- Alterar o valor padrao de `Messaging:Provider`.
- Remover Kafka.
- Usar Terraform para configurar o Pub/Sub emulator.

## Validacao esperada
A implementacao futura deve validar:

- build da solution;
- testes unitarios e de arquitetura dos workers;
- testes de integracao da Outbox para Kafka e Pub/Sub;
- testes de consumo idempotente, `ack`, `nack` e delivery attempts;
- testes de ordering key baseada no `AggregateId` quando aplicavel;
- testes que diferenciem DLQ tecnica do Pub/Sub e DLQ de aplicacao;
- validacao do Terraform para recursos reais na GCP;
- validacao do setup local com Pub/Sub emulator.
