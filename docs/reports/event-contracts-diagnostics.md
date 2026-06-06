# Diagnostico de contratos de eventos

Data: 2026-06-06

## Resumo executivo

O fluxo atual entre `LedgerService` e `BalanceService` tem um contrato de integracao principal: `LedgerEntryCreated.v1`. Ele e criado no Ledger, persistido no Outbox como payload JSON, publicado pelo `LedgerService.Worker` em Pub/Sub por padrao ou Kafka no modo legado, consumido pelo `BalanceService.Worker` e aplicado na projecao de saldos.

Foram encontrados tambem dois eventos operacionais no Outbox do Ledger: `LancamentoEstornoSolicitado.v1` e `ReprocessamentoLancamentosSolicitado.v1`. Eles nao fazem parte do fluxo Ledger para Balance. O reprocessamento possui consumer Kafka dentro do proprio `LedgerService.Worker`; estorno e processado por polling no banco, embora sua solicitacao tambem seja registrada e publicada pelo Outbox quando o provider publica todos os eventos mapeados.

O contrato de `LedgerEntryCreated.v1` ja possui documento, exemplo e JSON Schema em `docs/contracts/events/`, mas a execucao ainda depende de disciplina entre produtor, consumer e adapters. A separacao entre contrato logico e metadados tecnicos existe parcialmente por portas neutras (`ReceivedMessage`, `TransportMessageContext`, `IOutboxMessagePublisher`, `IDeadLetterPublisher`), mas o envelope de evento nao e formalizado como contrato independente.

## Eventos encontrados

| Evento | Natureza | Criado em | Consumido em | Observacao |
| --- | --- | --- | --- | --- |
| `LedgerEntryCreated.v1` | Integracao entre servicos | `CreateLancamentoCommandHandler`, `ProcessarEstornoLancamentoHandler`, `ProcessarReprocessamentoLancamentosHandler` via `LedgerEntryCreatedOutboxWriter` ou serializacao local | `BalanceService.Worker` por Pub/Sub ou Kafka | Contrato principal Ledger para Balance. |
| `LancamentoEstornoSolicitado.v1` | Operacional do Ledger | `SolicitarEstornoLancamentoHandler` | Nao foi encontrado consumer de mensageria | O processamento efetivo de estornos ocorre por polling em `EstornoLancamentoProcessorService`. |
| `ReprocessamentoLancamentosSolicitado.v1` | Operacional do Ledger | `SolicitarReprocessamentoLancamentosHandler` | `ReprocessamentoLancamentosConsumerService` no modo Kafka | Consumer existe apenas no adapter Kafka do `LedgerService.Worker`. |

## Fluxo Ledger para Balance

1. `LedgerService.Api` recebe a requisicao HTTP de lancamento e monta `CreateLancamentoInput` com `Idempotency-Key`, `X-Correlation-Id`, merchant e payload HTTP.
2. `CreateLancamentoCommandHandler` valida idempotencia HTTP, cria `LedgerEntry`, monta a resposta `LancamentoDto` e chama `LedgerEntryCreatedOutboxWriter`.
3. `LedgerEntryCreatedOutboxWriter` cria `LedgerEntryCreatedV1` com `LedgerEntryCreatedEventFactory`, serializa com `JsonSerializerDefaults.Web` e grava `OutboxMessage` na mesma transacao.
4. `LedgerService.Worker` executa `OutboxPublisherService`, faz claim de mensagens pendentes, resolve destino pelo provider selecionado e publica.
5. No Pub/Sub, `PubSubOutboxMessagePublisher` publica `PubsubMessage` com payload em `Data`, attributes e `OrderingKey` opcional.
6. No Kafka, `KafkaOutboxMessagePublisher` publica `Message<string,string>` com payload em `Value`, headers, key baseada no aggregate id e timestamp do Outbox.
7. `BalanceService.Worker` consome a mensagem pelo provider selecionado e mapeia o transporte para `ReceivedMessage`.
8. `LedgerEntryCreatedMessageProcessor` valida `event_type`, desserializa `LedgerEntryCreatedEvent`, rejeita campos desconhecidos, valida payload e chama `ApplyLedgerEntryCreatedHandler`.
9. `ApplyLedgerEntryCreatedHandler` usa `evt.Id` como chave de idempotencia em `processed_events`, aplica o saldo diario por `merchantId`, data derivada de `occurredAt` e moeda default `BRL`.
10. Mensagens invalidas ou falhas nao recuperaveis sao publicadas na DLQ de aplicacao do provider selecionado; no Kafka, o offset original so e commitado depois do processamento ou DLQ com sucesso.

## Payloads atuais

### LedgerEntryCreated.v1

Payload JSON atual:

| Campo | Tipo observado | Obrigatorio no consumer | Semantica |
| --- | --- | --- | --- |
| `id` | string | Sim | Identificador logico estavel do lancamento, no formato `lan_` mais 8 caracteres hexadecimais. |
| `type` | string | Sim | `CREDIT` ou `DEBIT`. |
| `amount` | string | Sim | Decimal com duas casas; positivo para credito e negativo para debito. |
| `createdAt` | string no produtor, `DateTimeOffset` no consumer | Sim | Instante de criacao no Ledger. |
| `merchantId` | string | Sim | Merchant usado para autorizacao e projecao. |
| `occurredAt` | string no produtor, `DateTimeOffset` no consumer | Sim | Instante do fato financeiro; seu offset define o dia consolidado. |
| `description` | string ou null | Nao | Descricao opcional. |
| `correlationId` | string UUID | Sim | Correlacao logica propagada desde a entrada HTTP ou fluxo operacional. |
| `externalReference` | string ou null | Nao | Referencia externa opcional. |

Ausencias relevantes:

- `currency` nao existe no evento. O Balance assume `BRL`.
- `eventVersion` nao existe como campo. A versao fica embutida em `event_type`.
- `eventName` nao existe como campo. O nome fica embutido em `event_type`.
- `eventId` nao existe como campo separado. O payload tem `id`, que representa o identificador logico do fato financeiro.
- `idempotencyKey` HTTP nao e propagada no evento.

### LancamentoEstornoSolicitado.v1

Payload serializado com `JsonSerializerDefaults.Web` a partir de um record sem `JsonPropertyName`, portanto em camelCase:

| Campo | Semantica |
| --- | --- |
| `estornoId` | Identificador da solicitacao de estorno. |
| `lancamentoOriginalId` | Identificador do lancamento original. |
| `merchantId` | Merchant do lancamento original. |
| `motivo` | Motivo informado na solicitacao. |
| `status` | Status inicial da solicitacao. |
| `requestedAt` | Instante de criacao da solicitacao. |
| `correlationId` | Correlacao logica da requisicao. |

### ReprocessamentoLancamentosSolicitado.v1

Payload serializado com `JsonSerializerDefaults.Web` a partir de um record sem `JsonPropertyName`, portanto em camelCase:

| Campo | Semantica |
| --- | --- |
| `reprocessamentoId` | Identificador da solicitacao de reprocessamento. |
| `merchantId` | Merchant a reprocessar. |
| `dataInicial` | Inicio do periodo. |
| `dataFinal` | Fim do periodo. |
| `motivo` | Motivo informado. |
| `status` | Status inicial da solicitacao. |
| `requestedAt` | Instante de criacao da solicitacao. |
| `correlationId` | Correlacao logica da requisicao. |

## Contrato logico e transporte

Contrato logico atual:

| Item | Representacao atual |
| --- | --- |
| Event name | Parte de `event_type`, por exemplo `LedgerEntryCreated`. |
| Event version | Parte de `event_type`, por exemplo `.v1`. |
| Event id | Para transporte, `event_id` recebe o `OutboxMessage.Id`; para dominio do Balance, `evt.Id` e usado como idempotencia. |
| Occurred at | No payload como `occurredAt` e no Outbox como `OccurredAt`; Kafka tambem usa esse valor como timestamp da mensagem. |
| Merchant id | No payload como `merchantId`. |
| Idempotency key | Existe no HTTP de escrita, mas nao no evento. No Balance, idempotencia e por `evt.Id`. |
| Currency | Nao existe no evento; `BRL` e default no Balance. |
| Correlation id | No payload como `correlationId` e tambem em attributes ou headers como `correlation_id`. |

Metadados logicos:

- `event_type`: nome e versao logica do evento.
- `correlation_id`: correlacao de negocio ou fluxo.
- `merchantId`: esta no payload porque participa da semantica do evento e da projecao.
- `occurredAt`: esta no payload porque define o fato financeiro e a data de consolidacao.

Metadados tecnicos:

- `event_id` em attributes ou headers, atualmente o id da linha de Outbox.
- `traceparent`, `tracestate` e `baggage`.
- Pub/Sub `message_id`, `publish_time`, `delivery_attempt`, `ordering_key`.
- Kafka topic, partition, offset, key, timestamp e commit.
- Campos de DLQ como `dlq_reason`, `original_source`, `original_provider`, `original_topic`, `original_partition`, `original_offset` e `original_metadata_*`.

Ponto de atencao: `correlationId` aparece no payload e tambem no transporte. Isso ajuda troubleshooting, mas cria risco de divergencia se produtor ou adapter passarem valores diferentes.

## Mapeamento Pub/Sub

| Item | Valor atual |
| --- | --- |
| Provider default | `Messaging:Provider=PubSub`. |
| Topic principal local | `ledger.ledgerentry.created.local`. |
| Topic GCP dev documentado | `ledger.ledgerentry.created.dev`. |
| Topic de DLQ de aplicacao local | `ledger.ledgerentry.created.dlq.local`. |
| Subscription do Balance local | `balance-service-ledger-events-local`. |
| Subscription de inspecao da DLQ local | `ledger-events-application-dlq-inspection-local`. |
| TopicMap do Ledger | Apenas `LedgerEntryCreated.v1` para `ledger.ledgerentry.created.local`. |
| Ordering key | Vazio por default, pois `EnableMessageOrdering=false`; se ligado, usa `AggregateId` sem hifens. |
| Ack | `LedgerEventsPubSubConsumer` retorna `Ack` quando o processor retorna `true`, inclusive apos publicar DLQ de aplicacao. |
| Nack | Retorna `Nack` em cancelamento ou excecao recuperavel nao tratada pelo processor. |
| Retry | Reentrega nativa por `Nack` e ack deadline; `ProcessingErrorRetryDelay` esta configurado, mas o consumer Pub/Sub atual nao aplica delay explicito no catch. |
| DLQ tecnica | Em ambiente local, nao e simulada no emulator. ADRs e docs distinguem DLQ tecnica futura da DLQ de aplicacao. |
| DLQ de aplicacao | `PubSubDeadLetterPublisher` publica um envelope `DeadLetterMessage` no topic configurado. |

Attributes publicados pelo Ledger:

| Attribute | Origem |
| --- | --- |
| `event_id` | `OutboxMessage.Id`. |
| `event_type` | `OutboxMessage.EventType`. |
| `correlation_id` | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Outbox ou Activity atual. |
| `tracestate` | Outbox ou Activity atual. |
| `baggage` | Outbox ou baggage atual. |

Metadados capturados pelo consumer Pub/Sub:

| Metadata | Origem |
| --- | --- |
| `subscription_id` | Configuracao do consumer. |
| `message_id` | Pub/Sub. |
| `ordering_key` | Pub/Sub. |
| `delivery_attempt` | Attribute `googclient_deliveryattempt`, quando presente. |
| `publish_time` | Pub/Sub. |

DLQ Pub/Sub de aplicacao:

- Payload da DLQ e o JSON de `DeadLetterMessage`.
- Attributes incluem `dlq_reason`, `original_source`, `original_provider`, `event_type`, `event_id`, `correlation_id`, W3C tracing e `original_metadata_*`.
- Nao ha commit de offset, pois Pub/Sub usa ack/nack.

## Mapeamento Kafka

| Item | Valor atual |
| --- | --- |
| Provider legado | `Messaging:Provider=Kafka` via `compose.kafka.yaml`. |
| Topic `LedgerEntryCreated.v1` | `ledger.ledgerentry.created`. |
| Topic `LancamentoEstornoSolicitado.v1` | `ledger.lancamento.estorno.solicitado`. |
| Topic `ReprocessamentoLancamentosSolicitado.v1` | `ledger.lancamentos.reprocessamento.solicitado`. |
| DLQ de aplicacao do Balance | `ledger.ledgerentry.created.dlq`. |
| Message key do producer | `AggregateId` sem hifens. |
| Partition | Nao definida explicitamente pelo producer; Kafka escolhe pela key e particionador. Localmente os topicos sao criados com 1 particao. |
| Offset | Capturado no consumer e propagado para metadados e DLQ. |
| Commit | Manual, com `EnableAutoCommit=false` e `consumer.Commit(result)` apos sucesso ou DLQ publicada com sucesso. |
| Retry de consumo | Erros de consume aguardam `ConsumeErrorRetryDelay`; erros de processamento recuperaveis aguardam `ProcessingErrorRetryDelay` e nao commitam. |
| Retry de publish | Controlado pelo Outbox com backoff exponencial e jitter. |

Headers publicados pelo Ledger:

| Header | Origem |
| --- | --- |
| `event_id` | `OutboxMessage.Id`. |
| `event_type` | `OutboxMessage.EventType`. |
| `correlation_id` | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Outbox ou Activity atual. |
| `tracestate` | Outbox ou Activity atual. |
| `baggage` | Outbox ou baggage atual. |

Metadados capturados pelo consumer Kafka:

| Metadata | Origem |
| --- | --- |
| `topic` | `ConsumeResult.Topic`. |
| `partition` | `ConsumeResult.Partition`. |
| `offset` | `ConsumeResult.Offset`. |
| `key` | `Message.Key`, quando presente. |

DLQ Kafka de aplicacao:

- Payload da DLQ e o JSON de `DeadLetterMessage`.
- Key da DLQ usa `originalTopic:originalPartition:originalOffset`.
- Headers incluem `dlq_reason`, `original_topic`, `original_partition`, `original_offset`, `event_type`, `event_id`, `correlation_id` e W3C tracing.
- O offset original e commitado apenas se a publicacao na DLQ concluir com sucesso.

## Outbox

`OutboxMessage` contem:

| Campo | Papel |
| --- | --- |
| `AggregateType` e `AggregateId` | Origem agregada e chave usada para Kafka key ou Pub/Sub ordering key opcional. |
| `EventType` | Nome versionado do evento. |
| `Payload` | JSON serializado. |
| `OccurredAt` | Instante do Outbox e timestamp usado no Kafka. |
| `Status` | `Pending`, `Processing`, `Processed` ou `DeadLetter`. |
| `RetryCount`, `NextRetryAt`, `LastError` | Retry e diagnostico de publicacao. |
| `CorrelationId`, `TraceParent`, `TraceState`, `Baggage` | Propagacao logica e tracing. |
| `LockedUntil`, `LockOwner` | Claim concorrente do worker. |
| `RequeueCount`, `LastRequeuedAt`, `LastRequeuedBy`, `LastRequeueReason` | Auditoria de requeue administrativo. |

O Outbox tenta publicar ate `MaxAttempts=10`, com `BaseBackoffSeconds=5`, backoff exponencial e jitter. Ao esgotar tentativas, a mensagem fica em `DeadLetter` no banco e pode ser requeued pelos endpoints administrativos do Ledger.

## Idempotencia

| Ponto | Estrategia |
| --- | --- |
| HTTP Ledger | `Idempotency-Key` por merchant e hash canonico do request, persistido em `IdempotencyRecord`. |
| Outbox | Sem deduplicacao por contrato de evento; cada linha de Outbox tem `Id` proprio. |
| Balance consumer | `processed_events.event_id` unico com `evt.Id`, nao com `event_id` de transporte. |
| Reprocessamento | Republica `LedgerEntryCreated.v1` com o mesmo `id` logico do lancamento para permitir deduplicacao no Balance. |
| DLQ | Preserva o payload e metadados originais, mas nao aplica redrive automatico. |

## Riscos atuais

1. `currency` ausente em `LedgerEntryCreated.v1` obriga `BRL` implicito no Balance e nas queries de saldo.
2. `event_id` de transporte representa o id da linha de Outbox, enquanto a idempotencia real do Balance usa `payload.id`; isso pode confundir observabilidade, DLQ e futuras regras de deduplicacao.
3. `event_type` e obrigatorio no transporte, mas nao existe envelope logico no payload. Uma mensagem com payload valido e attribute/header ausente vai para DLQ.
4. `correlationId` duplicado entre payload e transporte pode divergir sem validacao cruzada.
5. `LancamentoEstornoSolicitado.v1` e publicado no Kafka, mas nao ha consumer de mensageria encontrado. O processamento real de estorno usa polling em banco.
6. `ReprocessamentoLancamentosSolicitado.v1` tem consumer apenas Kafka. No modo Pub/Sub principal, nao ha adapter Pub/Sub equivalente para esse fluxo.
7. No Pub/Sub, o `TopicMap` versionado so mapeia `LedgerEntryCreated.v1`; eventos operacionais cairiam no `DefaultTopicId` se publicados nesse provider.
8. `ProcessingErrorRetryDelay` do Pub/Sub esta configurado, mas nao foi encontrado uso efetivo no catch do consumer Pub/Sub.
9. O consumer do Balance rejeita propriedades desconhecidas, entao adicionar campo opcional ao producer sem atualizar consumer e schema juntos pode quebrar consumo.
10. O event version fica apenas em string livre `EventType`, sem parser ou tipo comum que separe nome e versao.
11. `occurredAt` e gerado no momento de criacao do Ledger para lancamentos novos, e reutilizado em reprocessamento a partir do lancamento persistido. Isso e correto para replay, mas precisa permanecer explicito em contrato.
12. Kafka e Pub/Sub tem metadados diferentes e a normalizacao em `ReceivedMessage` nao elimina todos os riscos de semantica, como ordering, commit, delivery attempt e DLQ tecnica.

## Dividas tecnicas encontradas

- Formalizar um envelope logico de evento ou uma convencao documentada para nome, versao, id logico, id tecnico e correlacao.
- Decidir se `event_id` deve ser id tecnico de transporte, id logico de evento, ou ambos com nomes distintos.
- Criar governanca para evolucao de `LedgerEntryCreated.v2`, especialmente para `currency`.
- Alinhar fluxo de reprocessamento ao provider principal Pub/Sub ou documentar explicitamente que continua Kafka only.
- Revisar o papel de `LancamentoEstornoSolicitado.v1`, pois hoje o evento e persistido e publicado, mas o processamento nao depende do consumo dele.
- Diferenciar operacionalmente DLQ tecnica Pub/Sub e DLQ de aplicacao em todos os ambientes.
- Adicionar validacao automatizada que compare schema, producer e consumer antes de mudancas de contrato.
- Reavaliar se `OutboxMessage` deve continuar no Domain ou ser isolado como mecanismo de Application/Infrastructure em evolucao futura.

## Recomendacao de proximos passos

1. Definir vocabulario de contrato: `event_name`, `event_version`, `event_id` logico, `message_id` tecnico, `correlation_id`, `causation_id` se necessario.
2. Documentar uma matriz de compatibilidade para producer e consumer, incluindo ordem de rollout.
3. Criar testes de contrato para `LedgerEntryCreated.v1` comparando payload produzido, schema existente e desserializacao do Balance.
4. Planejar `LedgerEntryCreated.v2` somente quando houver suporte real a `currency` no HTTP, persistencia do Ledger, evento e consultas do Balance.
5. Decidir se reprocessamento deve ter adapter Pub/Sub no provider principal ou se o fluxo deve ser removido do caminho de mensageria e tratado como job interno.
6. Decidir se `LancamentoEstornoSolicitado.v1` deve ser consumido por mensageria, permanecer apenas como fato operacional auditavel, ou deixar de ser publicado externamente em etapa futura.
7. Padronizar os nomes dos metadados de DLQ entre providers sem perder detalhes nativos.

## Arquivos relevantes encontrados

Ledger API e Application:

- `src/LedgerService.Api/Controllers/LancamentosController.cs`
- `src/LedgerService.Api/Controllers/OutboxAdminController.cs`
- `src/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommandHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/SolicitarEstornoLancamentoHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/SolicitarReprocessamentoLancamentosHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/ProcessarEstornoLancamentoHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/ProcessarReprocessamentoLancamentosHandler.cs`
- `src/LedgerService.Application/Lancamentos/Events/LedgerEntryCreatedV1.cs`
- `src/LedgerService.Application/Lancamentos/Events/LancamentoEstornoSolicitadoV1.cs`
- `src/LedgerService.Application/Lancamentos/Events/ReprocessamentoLancamentosSolicitadoV1.cs`
- `src/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedEventFactory.cs`
- `src/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedOutboxWriter.cs`

Ledger Domain, Infrastructure e Worker:

- `src/LedgerService.Domain/Entities/OutboxMessage.cs`
- `src/LedgerService.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs`
- `src/LedgerService.Worker/Outbox/OutboxPublisherService.cs`
- `src/LedgerService.Worker/Outbox/OutboxPublisherOptions.cs`
- `src/LedgerService.Worker/Messaging/Abstractions/IOutboxMessagePublisher.cs`
- `src/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs`
- `src/LedgerService.Worker/Messaging/PubSub/Producers/PubSubOutboxMessagePublisher.cs`
- `src/LedgerService.Worker/Messaging/Kafka/Consumers/ReprocessamentoLancamentosConsumerService.cs`
- `src/LedgerService.Worker/Messaging/Kafka/Consumers/KafkaReprocessamentoReceivedMessageMapper.cs`
- `src/LedgerService.Worker/Messaging/Processors/ReprocessamentoLancamentosMessageProcessor.cs`
- `src/LedgerService.Worker/Estornos/EstornoLancamentoProcessorService.cs`
- `src/LedgerService.Worker/Extensions/WorkerCompositionExtensions.cs`
- `src/LedgerService.Worker/appsettings.json`
- `src/LedgerService.Worker/appsettings.PubSub.json`

Balance Application, Domain, Infrastructure e Worker:

- `src/BalanceService.Application/Balances/Commands/ApplyLedgerEntryCreatedHandler.cs`
- `src/BalanceService.Application/Balances/Queries/GetDailyBalanceHandler.cs`
- `src/BalanceService.Application/Balances/Queries/GetPeriodBalanceHandler.cs`
- `src/BalanceService.Domain/Balances/LedgerEntryCreatedEvent.cs`
- `src/BalanceService.Domain/Balances/ProcessedEvent.cs`
- `src/BalanceService.Domain/Balances/DailyBalance.cs`
- `src/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs`
- `src/BalanceService.Worker/Messaging/Abstractions/ReceivedMessage.cs`
- `src/BalanceService.Worker/Messaging/Abstractions/DeadLetterMessage.cs`
- `src/BalanceService.Worker/Messaging/Abstractions/IDeadLetterPublisher.cs`
- `src/BalanceService.Worker/Messaging/Contracts/LedgerEntryCreatedV1Contract.cs`
- `src/BalanceService.Worker/Messaging/Processors/LedgerEntryCreatedMessageProcessor.cs`
- `src/BalanceService.Worker/Messaging/PubSub/Consumers/LedgerEventsPubSubConsumer.cs`
- `src/BalanceService.Worker/Messaging/PubSub/Consumers/PubSubReceivedMessageMapper.cs`
- `src/BalanceService.Worker/Messaging/PubSub/DeadLetter/PubSubDeadLetterPublisher.cs`
- `src/BalanceService.Worker/Messaging/Kafka/Consumers/LedgerEventsConsumer.cs`
- `src/BalanceService.Worker/Messaging/Kafka/Consumers/KafkaReceivedMessageMapper.cs`
- `src/BalanceService.Worker/Messaging/Kafka/DeadLetter/KafkaDeadLetterPublisher.cs`
- `src/BalanceService.Worker/Extensions/WorkerCompositionExtensions.cs`
- `src/BalanceService.Worker/appsettings.json`
- `src/BalanceService.Worker/appsettings.PubSub.json`

Documentacao e configuracao:

- `docs/contracts/events/LedgerEntryCreated.v1.md`
- `docs/contracts/events/LedgerEntryCreated.v1.schema.json`
- `docs/contracts/events/LedgerEntryCreated.v1.example.json`
- `docs/adrs/0075-mensageria-ports-adapters-kafka-provider.md`
- `docs/adrs/0076-formalizar-contrato-ledger-entry-created-v1.md`
- `docs/adrs/0077-pubsub-provider-mensageria.md`
- `docs/adrs/0078-pubsub-provider-principal-local-emulator.md`
- `docs/adrs/0070-dlq-outbox-banco-backoff-requeue.md`
- `docs/development/kafka-outbox.md`
- `docs/development/local-development.md`
- `compose.yaml`
- `compose.kafka.yaml`

## Validacoes executadas

Foram executados:

```powershell
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
```

Resultados:

| Comando | Resultado |
| --- | --- |
| `dotnet restore ./LedgerService.slnx` | Passou. Todos os projetos estavam atualizados para restauracao. |
| `dotnet build ./LedgerService.slnx --configuration Release --no-restore` | Passou. Build Release com 0 avisos e 0 erros. |
