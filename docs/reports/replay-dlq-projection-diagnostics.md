# Diagnostico de replay, DLQ e projecao

Data: 2026-06-07

> Nota de atualizacao: este relatorio e historico. A decisao sobre provider
> principal/default foi alterada posteriormente pela
> [ADR-0088](../adrs/0088-kafka-default-ledger-balance-workers.md): Kafka e o
> provider padrao dos workers principais e Pub/Sub permanece explicito/legado.

## Resumo executivo

O projeto ja possui uma base operacional importante: Outbox transacional no Ledger, publicacao por `LedgerService.Worker`, consumo idempotente no Balance, DLQ de aplicacao para mensagens rejeitadas pelo `BalanceService.Worker` e requeue administrativo da DLQ em banco da Outbox. No momento do diagnostico, Pub/Sub era tratado como default; conforme nota acima, a decisao atual e Kafka como provider padrao e Pub/Sub explicito/legado.

O estado atual ainda nao equivale a uma operacao completa de replay, redrive e reconstrucao de projecao. Existe replay de fatos financeiros persistidos no Ledger para o fluxo de reprocessamento de lancamentos, mas esse fluxo hoje depende do consumer Kafka de `ReprocessamentoLancamentosSolicitado.v1` e nao e um redrive generico de DLQ. Nao foi encontrado redrive versionado de DLQ Pub/Sub ou Kafka. Tambem nao foi encontrada reconstrucao completa da projecao `daily_balances`; o replay atual corrige eventos ausentes, mas nao recalcula saldos ja materializados com regra historica incorreta.

O maior ponto positivo e a idempotencia do Balance: `ApplyLedgerEntryCreatedHandler` registra `payload.id` em `processed_events.event_id` com indice unico e `INSERT ... ON CONFLICT DO NOTHING` antes de alterar saldo. Isso reduz o risco de saldo duplicado em reentrega, requeue da Outbox e replay de fatos do Ledger, desde que o `payload.id` seja preservado.

## Fluxo atual de publicacao

1. `LedgerService.Api` recebe comandos HTTP de lancamento, estorno ou reprocessamento.
2. Os handlers de Application persistem a mudanca de negocio e gravam uma linha em `ledger.outbox_messages` na mesma transacao.
3. `OutboxMessage` nasce como `Pending` e carrega `AggregateType`, `AggregateId`, `EventType`, `Payload`, `OccurredAt`, `CorrelationId`, contexto W3C e campos de retry.
4. `LedgerService.Worker` hospeda `OutboxPublisherService`.
5. O worker reclama mensagens elegiveis com lock temporario, marca como `Processing`, salva o claim e publica em paralelo usando `IOutboxMessagePublisher`.
6. O adapter concreto e escolhido por `Messaging:Provider`; na decisao atual, Kafka e o default e Pub/Sub e ativado explicitamente como modo legado/alternativo.
7. Em sucesso de publish, a mensagem vira `Processed`.
8. Em falha de publish, o worker incrementa `RetryCount`, registra `LastError`, calcula `NextRetryAt` por backoff exponencial com jitter e devolve para `Pending`.
9. Ao atingir `Outbox:Publisher:MaxAttempts`, a mensagem vira `DeadLetter` no banco e sai do processamento automatico.

Eventos financeiros finais usam `LedgerEntryCreated.v2` no codigo atual. O consumer do Balance ainda aceita `LedgerEntryCreated.v1` como legado.

## Fluxo atual de consumo

1. `BalanceService.Worker` seleciona o consumer por `Messaging:Provider`.
2. No Pub/Sub, `LedgerEventsPubSubConsumer` recebe mensagens da subscription configurada e mapeia para `ReceivedMessage`.
3. No Kafka, `LedgerEventsConsumer` consome topicos configurados, mapeia topic, partition, offset, key e headers para `ReceivedMessage`.
4. Ambos chamam `LedgerEntryCreatedMessageProcessor`.
5. O processor valida `event_type`, aceita somente `LedgerEntryCreated.v1` ou `LedgerEntryCreated.v2`, desserializa com rejeicao de propriedades desconhecidas e valida campos obrigatorios.
6. Mensagem valida chama `ApplyLedgerEntryCreatedHandler`.
7. O handler abre transacao, tenta inserir `ProcessedEvent` por `evt.Id`, bloqueia o saldo diario por merchant, data e moeda, cria ou atualiza `DailyBalance`, salva e commita.
8. Mensagem duplicada, com mesmo `evt.Id`, e tratada como sucesso idempotente e nao altera saldo.
9. Mensagem invalida ou erro nao recuperavel e enviada para DLQ de aplicacao pelo `IDeadLetterPublisher`. Depois disso o processor retorna `true`, permitindo ack ou commit.

## Estado atual de DLQ no Pub/Sub

Ha duas categorias documentadas:

| Categoria | Estado atual |
| --- | --- |
| DLQ de aplicacao | Implementada no `BalanceService.Worker` por `PubSubDeadLetterPublisher`. Publica `DeadLetterMessage` no topic `PubSub:Consumer:DeadLetterTopicId`. |
| DLQ tecnica nativa | Prevista e provisionavel via Terraform para GCP real, mas nao simulada no emulator local. A aplicacao nao publica diretamente nessa DLQ. |

No consumo Pub/Sub, `LedgerEventsPubSubConsumer` retorna `Ack` quando o processor retorna `true`, inclusive depois de publicar DLQ de aplicacao. Retorna `Nack` quando o processor retorna `false`, quando ha cancelamento durante processamento ou quando uma excecao recuperavel escapa do processor.

O envelope de DLQ de aplicacao preserva payload original, source, provider, `event_type`, motivo, tipo da excecao, attributes originais e metadados de transporte com prefixo `original_metadata_`.

Nao foi encontrado redrive implementado para mensagens publicadas na DLQ de aplicacao Pub/Sub.

## Estado atual de DLQ no Kafka

O caminho Kafka possui DLQ de aplicacao no `BalanceService.Worker` por `KafkaDeadLetterPublisher`.

O publisher de DLQ:

- serializa `DeadLetterMessage` como payload da DLQ;
- usa key `originalTopic:originalPartition:originalOffset`;
- adiciona headers `dlq_reason`, `original_topic`, `original_partition`, `original_offset`, `event_type`, `event_id`, `correlation_id` e headers W3C quando presentes;
- usa producer com `Acks.All` e `EnableIdempotence=true`;
- registra metricas de DLQ e erro de publish.

O consumer Kafka usa `EnableAutoCommit=false` e `EnableAutoOffsetStore=false`. O offset original so e commitado quando `LedgerEntryCreatedMessageProcessor.ProcessAsync` retorna `true`. Como o processor so retorna `true` apos sucesso funcional, duplicidade idempotente ou DLQ publicada com sucesso, uma falha ao publicar na DLQ impede o commit e permite nova tentativa.

Nao foi encontrado redrive implementado para mensagens publicadas no topico de DLQ Kafka.

## Estado atual de retry

| Area | Estado atual |
| --- | --- |
| Publicacao da Outbox | Retry implementado em banco, com `RetryCount`, `NextRetryAt`, `LastError`, backoff exponencial e jitter. Limite default `MaxAttempts=10`. |
| Claim da Outbox | `Processing` com `LockOwner` e `LockedUntil`; locks expirados podem ser reclamados novamente. |
| Pub/Sub consume | Reentrega depende de `Nack` e ack deadline do Pub/Sub. `ProcessingErrorRetryDelay` e validado nas options, mas nao foi encontrado uso efetivo de delay no catch do consumer Pub/Sub. |
| Kafka consume | `ConsumeException` aguarda `ConsumeErrorRetryDelay`; erros recuperaveis de processamento aguardam `ProcessingErrorRetryDelay` e nao commitam offset. |
| Mensagem invalida no Balance | Nao passa por retry indefinido. E classificada como DLQ de aplicacao e o transporte e confirmado depois da DLQ bem sucedida. |
| Reprocessamento Kafka do Ledger | Retry por nao commit em falhas tecnicas e delays configurados. Mensagens invalidas sao apenas logadas e tratadas como consumidas, sem DLQ especifica. |

## Estado atual de replay

Existe replay funcional de fatos financeiros persistidos no Ledger no fluxo de reprocessamento de lancamentos:

1. `POST /api/v1/lancamentos/reprocessar` cria `ReprocessamentoLancamentos` e grava `ReprocessamentoLancamentosSolicitado.v1` no Outbox.
2. No modo Kafka, `ReprocessamentoLancamentosConsumerService` consome o topico `ledger.lancamentos.reprocessamento.solicitado`.
3. `ReprocessamentoLancamentosMessageProcessor` valida source, `event_type` e payload.
4. `ProcessarReprocessamentoLancamentosHandler` busca lancamentos do merchant e periodo informados.
5. Para cada lancamento encontrado, grava um novo `LedgerEntryCreated.v2` no Outbox.
6. O Balance recebe o evento pelo fluxo normal e aplica ou ignora como duplicado.

Esse replay e limitado. Ele corrige eventos ausentes na projecao quando o `payload.id` ainda nao existe em `processed_events`. Ele nao recalcula saldos ja aplicados com o mesmo `payload.id`.

No provider Pub/Sub explicito/legado, nao foi encontrado consumer equivalente para `ReprocessamentoLancamentosSolicitado.v1`.

## Estado atual de redrive de DLQ

Nao foi encontrado redrive implementado para DLQ de aplicacao Pub/Sub ou Kafka.

Existe requeue administrativo da DLQ em banco da Outbox:

- `GET /api/v1/outbox/dead-letters`;
- `POST /api/v1/outbox/dead-letters/{id}/requeue`;
- ambos exigem scope `outbox.admin`;
- o requeue atua somente sobre mensagens `OutboxStatus.DeadLetter`;
- volta a mensagem para `Pending`, zera `RetryCount`, limpa `LastError`, libera lock e registra `RequeueCount`, `LastRequeuedAt`, `LastRequeuedBy` e `LastRequeueReason`.

Esse mecanismo corrige falhas de publicacao da Outbox. Ele nao redriva mensagens que ja foram consumidas pelo Balance e enviadas para DLQ de aplicacao.

## Estado atual de reconstrucao de projecao

Nao foi encontrada reconstrucao completa da projecao `daily_balances`.

O Balance mantem uma projecao incremental:

- `daily_balances` armazena saldos por merchant, data e moeda;
- `processed_events` registra eventos ja aplicados;
- `ApplyLedgerEntryCreatedHandler` atualiza a projecao no consumo de cada evento.

O replay de reprocessamento do Ledger pode preencher lacunas quando um evento financeiro nunca foi aplicado no Balance. Se o evento ja foi aplicado, o mesmo `payload.id` sera ignorado por idempotencia, mesmo que a regra antiga de consolidacao estivesse incorreta. Para recompor saldo ja materializado, seria necessaria uma decisao nova, por exemplo rebuild controlado da projecao, evento de correcao ou truncamento e recomputacao com auditoria.

## Estado atual de idempotencia

| Ponto | Estrategia |
| --- | --- |
| HTTP no Ledger | `Idempotency-Key` por merchant e hash canonico do request, com replay de resposta quando a chave e o payload sao iguais. |
| Outbox | Nao deduplica por contrato logico. Cada linha tem `OutboxMessage.Id` proprio. |
| Transporte | Pub/Sub e Kafka operam como at-least-once no desenho atual. Duplicatas sao esperadas. |
| Balance | Deduplicacao por `evt.Id`, que vem do `id` do payload financeiro. `processed_events.event_id` e unico. |
| Persistencia do Balance | `ProcessedEventRepository.TryInsertAsync` usa `INSERT ... ON CONFLICT (event_id) DO NOTHING`. |
| Reprocessamento | Gera `LedgerEntryCreated.v2` com id financeiro derivado do lancamento original, preservando deduplicacao no Balance. |

## Eventos duplicados podem gerar saldo duplicado?

No fluxo normal, nao deveriam gerar saldo duplicado se o `payload.id` for preservado.

O handler do Balance tenta registrar o evento processado antes de atualizar saldo. Se o evento ja existir em `processed_events`, a transacao e commitada sem alterar `daily_balances`. Isso protege contra reentrega do broker, requeue da Outbox, publish duplicado e replay de fatos ja aplicados.

Riscos remanescentes:

- se um redrive futuro alterar o `payload.id`, a idempotencia sera bypassada;
- se dois fatos distintos forem produzidos com o mesmo `payload.id`, o segundo sera ignorado;
- se uma recomposicao futura apagar `processed_events` sem estrategia transacional para `daily_balances`, pode haver duplicacao ou perda;
- o `event_id` tecnico de transporte e o `payload.id` logico tem semanticas diferentes, o que pode induzir erro operacional em redrive manual.

## Mensagens invalidas e erros transitorios

O Balance diferencia mensagens invalidas de falhas recuperaveis principalmente no `LedgerEntryCreatedMessageProcessor`.

Mensagens invalidas ou nao recuperaveis:

- JSON invalido;
- `event_type` ausente ou nao suportado;
- propriedades desconhecidas no payload;
- campos obrigatorios ausentes;
- `currency` invalida ou ausente em v2;
- `currency` presente indevidamente em v1;
- falha de dominio ou argumento classificada como nao recuperavel.

Esses casos sao enviados para DLQ de aplicacao e depois confirmados no transporte.

Erros transitorios ou recuperaveis:

- `DbUpdateException` no consumer Kafka do Balance;
- `TimeoutException`;
- `KafkaException`;
- excecao inesperada que escape do processor Pub/Sub;
- falha de publish da Outbox.

Esses casos nao devem confirmar a mensagem original. No Kafka, o offset nao e commitado. No Pub/Sub, o consumer retorna `Nack`. Na Outbox, a mensagem volta para `Pending` com `NextRetryAt` ou vira `DeadLetter` apos o limite.

Ponto de atencao: o consumer Kafka de reprocessamento do Ledger loga mensagens invalidas e retorna sucesso, sem DLQ especifica para `ReprocessamentoLancamentosSolicitado.v1`.

## Registro operacional de falha

| Area | Registro atual |
| --- | --- |
| Outbox | `RetryCount`, `NextRetryAt`, `LastError`, `ProcessedAt`, `LockOwner`, `LockedUntil`, `CorrelationId`, `TraceParent`, `TraceState`, `Baggage`, dados de requeue. |
| Outbox logs | Logs de claim, sucesso, falha de publish e movimento para `DeadLetter`. |
| Pub/Sub DLQ | Envelope `DeadLetterMessage`, attributes `dlq_reason`, `original_source`, `original_provider`, event metadata e `original_metadata_*`. |
| Kafka DLQ | Envelope `DeadLetterMessage`, headers com motivo, origem, partition, offset, event metadata e tracing. |
| Balance consumer | Logs com provider, source, partition, offset quando existem, motivo e exception. |
| Balance idempotencia | `processed_events` registra `EventId`, `MerchantId`, `OccurredAt` e `ProcessedAt`. |
| Metricas | Outbox, consumo, duplicidade, DLQ e erros possuem metricas customizadas, com tags de baixa cardinalidade. |

Lacunas:

- nao ha uma tabela operacional dedicada para redrive de DLQ de aplicacao;
- nao ha trilha persistente de decisao de descarte ou redrive de mensagens da DLQ de aplicacao;
- a DLQ de aplicacao depende do envelope no broker e da retencao do provider;
- o reprocessamento Kafka do Ledger nao possui DLQ propria para mensagens invalidas.

## Riscos encontrados

1. Nao existe redrive versionado de DLQ Pub/Sub ou Kafka, apenas runbook.
2. Nao existe reconstrucao completa de `daily_balances`.
3. O replay atual depende do Kafka para a solicitacao de reprocessamento, alinhado ao provider padrao definido pela ADR-0088.
4. Requeue da Outbox pode ser confundido com redrive de DLQ do Balance, mas atua em momentos diferentes do fluxo.
5. `event_id` de transporte representa `OutboxMessage.Id`, enquanto a idempotencia real do Balance usa `payload.id`.
6. Redrive manual que altere `payload.id` pode duplicar saldo.
7. Redrive manual que preserve `payload.id` nao corrige saldo ja aplicado com regra antiga incorreta.
8. Pub/Sub local nao simula DLQ tecnica nativa, entao ha diferenca entre emulator e GCP real.
9. `ProcessingErrorRetryDelay` do Pub/Sub e configurado, mas nao aplicado explicitamente no catch do consumer atual.
10. O consumer Kafka de reprocessamento ignora mensagens invalidas sem DLQ de aplicacao dedicada.
11. Nao ha validador runtime compartilhado de JSON Schema antes de redrive manual.
12. Nao ha mecanismo de dry-run, limite de lote, auditoria e criterio de parada para redrive ou rebuild.

## Riscos de reprocessamento inseguro

- Republicar mensagem de DLQ sem validar `event_type` e schema pode recolocar poison message no fluxo principal.
- Reprocessar em lote sem limite pode saturar Outbox, broker ou locks do Balance.
- Alterar payload para "corrigir" contrato sem decisao formal cria divergencia de auditoria.
- Apagar `processed_events` para forcar replay pode duplicar saldos existentes.
- Redrive Kafka com commit antecipado do topico de DLQ pode perder mensagem se a republicacao falhar.
- Ack indevido de mensagem Pub/Sub de DLQ antes da persistencia da decisao operacional pode perder evidencia.
- Misturar DLQ tecnica Pub/Sub com DLQ de aplicacao pode levar a diagnostico errado de contrato.

## Dividas tecnicas

- Implementar consumer Pub/Sub para `ReprocessamentoLancamentosSolicitado.v1` somente se uma decisao futura exigir esse suporte no modo legado/alternativo.
- Criar ferramenta versionada de redrive de DLQ de aplicacao para Pub/Sub e Kafka.
- Definir uma estrategia de rebuild de projecao, com auditoria, dry-run e isolamento transacional.
- Persistir decisoes operacionais de DLQ, como discard, redrive, operador, motivo e resultado.
- Padronizar nomes entre id tecnico de mensagem, id da Outbox e id logico do fato financeiro.
- Adicionar validacao runtime de contrato antes de redrive, usando os schemas versionados.
- Dar DLQ ou politica explicita para mensagens invalidas do consumer de reprocessamento do Ledger.
- Alinhar metricas de DLQ Pub/Sub com a mesma visibilidade ja existente no Kafka.
- Documentar playbooks separados para Outbox requeue, DLQ redrive e projection rebuild.

## Lacunas entre Pub/Sub e Kafka

| Tema | Pub/Sub | Kafka |
| --- | --- | --- |
| Provider default | Principal. | Legado opcional. |
| Publicacao Ledger | Adapter `PubSubOutboxMessagePublisher`. | Adapter `KafkaOutboxMessagePublisher`. |
| Consumo Balance | `Ack` ou `Nack`. | Commit manual de offset. |
| Retry de consume | Redelivery por `Nack` e ack deadline. | Nao commit mais delay configurado. |
| DLQ de aplicacao | Topic configurado em `DeadLetterTopicId`. | Topic configurado em `DeadLetterTopic`. |
| DLQ tecnica | Prevista via Terraform no Pub/Sub real. | Nao ha equivalente nativo no desenho atual. |
| Reprocessamento Ledger | Consumer Pub/Sub nao encontrado. | Consumer Kafka implementado. |
| Metadados nativos | `message_id`, `publish_time`, `ordering_key`, `delivery_attempt`. | topic, partition, offset, key, timestamp. |
| Redrive | Nao implementado. | Nao implementado. |
| Emulator local | Nao simula DLQ tecnica nativa. | Compose legado cria topicos locais. |

## Proximos passos recomendados

1. Separar formalmente tres operacoes: requeue da Outbox, redrive de DLQ de aplicacao e rebuild de projecao.
2. Manter o reprocessamento no provider Kafka padrao ou decidir explicitamente o destino do consumer Pub/Sub de reprocessamento.
3. Desenhar redrive com validacao de schema, dry-run, limite de lote, preservacao de `payload.id`, auditoria e metricas.
4. Definir rebuild de projecao com fonte de verdade, janela, isolamento, tratamento de `processed_events` e estrategia de rollback.
5. Criar uma matriz operacional de causas: falha transitoria, contrato invalido, payload poison, duplicidade esperada e regra nao recuperavel.
6. Padronizar nomenclatura operacional para `outbox_message_id`, `transport_event_id` e `payload_event_id`.
7. Adicionar DLQ ou descarte auditado para mensagens invalidas do fluxo de reprocessamento do Ledger.
8. Criar testes de contrato e integracao cobrindo redelivery, DLQ publicada, commit ou ack e duplicidade idempotente nos dois providers.

## Arquivos relevantes analisados

Ledger:

- `src/LedgerService.Api/Controllers/OutboxAdminController.cs`
- `src/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommandHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/SolicitarReprocessamentoLancamentosHandler.cs`
- `src/LedgerService.Application/Lancamentos/Commands/ProcessarReprocessamentoLancamentosHandler.cs`
- `src/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedOutboxWriter.cs`
- `src/LedgerService.Application/Outbox/Commands/RequeueDeadLetterHandler.cs`
- `src/LedgerService.Application/Outbox/Queries/GetDeadLettersHandler.cs`
- `src/LedgerService.Application/Outbox/Retry/ExponentialBackoffRetryStrategy.cs`
- `src/LedgerService.Domain/Entities/OutboxMessage.cs`
- `src/LedgerService.Domain/Entities/OutboxStatus.cs`
- `src/LedgerService.Infrastructure/Persistence/Repositories/OutboxMessageRepository.cs`
- `src/LedgerService.Worker/Outbox/OutboxPublisherService.cs`
- `src/LedgerService.Worker/Outbox/OutboxPublisherOptions.cs`
- `src/LedgerService.Worker/Messaging/PubSub/Producers/PubSubOutboxMessagePublisher.cs`
- `src/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs`
- `src/LedgerService.Worker/Messaging/Kafka/Consumers/ReprocessamentoLancamentosConsumerService.cs`
- `src/LedgerService.Worker/Messaging/Processors/ReprocessamentoLancamentosMessageProcessor.cs`
- `src/LedgerService.Worker/Extensions/WorkerCompositionExtensions.cs`
- `src/LedgerService.Worker/appsettings.json`

Balance:

- `src/BalanceService.Application/Balances/Commands/ApplyLedgerEntryCreatedHandler.cs`
- `src/BalanceService.Domain/Balances/ProcessedEvent.cs`
- `src/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs`
- `src/BalanceService.Infrastructure/Persistence/Repositories/ProcessedEventRepository.cs`
- `src/BalanceService.Worker/Messaging/Abstractions/DeadLetterMessage.cs`
- `src/BalanceService.Worker/Messaging/Processors/LedgerEntryCreatedMessageProcessor.cs`
- `src/BalanceService.Worker/Messaging/PubSub/Consumers/LedgerEventsPubSubConsumer.cs`
- `src/BalanceService.Worker/Messaging/PubSub/DeadLetter/PubSubDeadLetterPublisher.cs`
- `src/BalanceService.Worker/Messaging/Kafka/Consumers/LedgerEventsConsumer.cs`
- `src/BalanceService.Worker/Messaging/Kafka/DeadLetter/KafkaDeadLetterPublisher.cs`
- `src/BalanceService.Worker/Extensions/WorkerCompositionExtensions.cs`
- `src/BalanceService.Worker/appsettings.json`

Documentacao:

- `docs/development/kafka-outbox.md`
- `docs/operations/event-replay-and-dlq.md`
- `docs/operations/pubsub.md`
- `docs/reports/event-contracts-diagnostics.md`
- `docs/adrs/0052-processamento-assincrono-reprocessamento-lancamentos-ledger.md`
- `docs/adrs/0070-dlq-outbox-banco-backoff-requeue.md`
- `docs/adrs/0075-mensageria-ports-adapters-kafka-provider.md`
- `docs/adrs/0077-pubsub-provider-mensageria.md`
- `docs/adrs/0078-pubsub-provider-principal-local-emulator.md`
- `docs/adrs/0084-ledger-entry-created-v2-currency-explicita.md`

## Validacoes executadas

| Comando | Resultado |
| --- | --- |
| `dotnet restore ./LedgerService.slnx` | Passou. Todos os projetos estavam atualizados para restauracao. |
| `dotnet build ./LedgerService.slnx --configuration Release --no-restore` | Passou com 361 avisos existentes e 0 erros. |

Os avisos observados sao de analisadores ja presentes no build, como CA1848, CA1062, CA1873, xUnit1051 e similares. Nao houve falha ambiental.
